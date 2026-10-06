// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects
{
    /// <summary>
    /// Represents a hit object which requires pressing, holding, and releasing a key.
    /// </summary>
    public class HoldNote : ManiaHitObject, IHasDuration
    {
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        private double duration;

        public double Duration
        {
            get => duration;
            set
            {
                duration = value;

                Tail?.StartTime = EndTime;
            }
        }

        public override double StartTime
        {
            get => base.StartTime;
            set
            {
                base.StartTime = value;

                Head?.StartTime = value;

                Tail?.StartTime = EndTime;
            }
        }

        public override int Column
        {
            get => base.Column;
            set
            {
                base.Column = value;

                Head?.Column = value;

                Tail?.Column = value;

                if (Ticks != null)
                {
                    foreach (var tick in Ticks)
                        tick.Column = value;
                }
            }
        }

        public IList<IList<HitSampleInfo>> NodeSamples { get; set; }

        public override HoldNote Clone()
        {
            var clone = (HoldNote)base.Clone();

            // 头尾与 tick 是 ApplyDefaults 造出来的、指向原对象嵌套实例的字段：留着它们会让副本的
            // Column/StartTime 写入传导到原对象的嵌套对象上。置空后由 ApplyDefaults 重建。
            clone.Head = null;
            clone.Tail = null;
            clone.Body = null;
            clone.Ticks = null;

            // NodeSamples 是普通属性，MemberwiseClone 只复制引用；外层与内层列表都要复制才不共享。
            clone.NodeSamples = NodeSamples?.Select(static node => (IList<HitSampleInfo>)node.ToList()).ToList();

            return clone;
        }

        /// <summary>
        /// The head note of the hold.
        /// </summary>
        public HeadNote Head { get; protected set; }

        /// <summary>
        /// The tail note of the hold.
        /// </summary>
        public TailNote Tail { get; protected set; }

        /// <summary>
        /// The body of the hold.
        /// This is an invisible and silent object that tracks the holding state of the <see cref="HoldNote"/>.
        /// </summary>
        public HoldNoteBody Body { get; protected set; }

        /// <summary>
        /// EZ2AC 风格 16 分 LN ticks（头格由 Head 承担，此处从下一格起）。
        /// 非 EZ2AC 下判定会被绑成 Ignore。
        /// </summary>
        public List<HoldNoteTick> Ticks { get; private set; }

        /// <summary>
        /// 按 hitmode 补齐 EZ2AC 的 16 分 tick（可重复调用，已有 tick 则直接返回）。
        /// </summary>
        /// <remarks>
        /// 生成时机有两处，缺一不可：
        /// <list type="bullet">
        /// <item>转换期（<see cref="ApplyDefaultsToSelf"/>）：此时只能按「当时的全局设置」判 EZ2AC，live / 编辑器靠这一处拿到 tick；</item>
        /// <item>绑定期（<see cref="EzMania.ReplayJudge.ManiaEnvironmentJudgements"/>）：按绑定的 hitmode 补生成。</item>
        /// </list>
        /// 后者是为了让 tick 集合取决于 hitmode 而不是转换时机：按 EZ2AC 作用域取出的仿真 / 分析副本，
        /// 若在全局设置不是 EZ2AC 时转换，转换期不会生成 tick，只有这里补上才不会把 EZ2AC 的 LN 算成官方头尾语义。
        /// </remarks>
        public void EnsureEz2AcTicks(CancellationToken cancellationToken = default)
        {
            // 消融开关在 DEBUG 下要求「全局不生成 tick」，两处都必须遵守，否则测量会自相矛盾。
            if (ManiaHoldAblation.DisableHoldTickGeneration)
                return;

            Ticks ??= new List<HoldNoteTick>();

            if (Ticks.Count > 0)
                return;

            createTicks(cancellationToken);
        }

        /// <summary>
        /// Whether sliding samples should be played when held.
        /// </summary>
        public bool PlaySlidingSamples { get; init; }

        public override double MaximumJudgementOffset => Tail.MaximumJudgementOffset;

        /// <summary>头时刻拍长（ms），用于生成 16 分 tick。</summary>
        private double beatLength = 500;

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            TimingControlPoint timingPoint = controlPointInfo.TimingPointAt(StartTime);
            beatLength = timingPoint.BeatLength;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            // Generally node samples will be populated by ManiaBeatmapConverter, but in a case like the editor they may not be.
            // Ensure they are set to a sane default here.
            NodeSamples ??= CreateDefaultNodeSamples(this);

            AddNested(Head = new HeadNote
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0),
            });

            AddNested(Tail = new TailNote
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples(NodeSamples.Count - 1),
            });

            AddNested(Body = new HoldNoteBody
            {
                StartTime = StartTime,
                Column = Column,
                Duration = Duration
            });

            Ticks = new List<HoldNoteTick>();

            // 仅 EZ2AC HitMode 生成 16 分 tick；其它模式保持官方头尾松手语义。
#if DEBUG
            if (!ManiaHoldAblation.DisableHoldTickGeneration
                && (ManiaHoldAblation.ForceHoldTickGeneration || isEz2AcHitMode()))
                createTicks(cancellationToken);
#else
            if (isEz2AcHitMode())
                createTicks(cancellationToken);
#endif
        }

        private static bool isEz2AcHitMode()
        {
            try
            {
                return GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode) == EzEnumHitMode.EZ2AC;
            }
            catch
            {
                return false;
            }
        }

        private void createTicks(CancellationToken cancellationToken)
        {
            // 16 分音符间隔；头格由 Head 计判定+combo，tick 从下一格起只推 combo。
            double interval = beatLength / 4;

            if (interval <= 0 || double.IsNaN(interval) || double.IsInfinity(interval))
                interval = 125;

            double t = StartTime + interval;
            const double end_epsilon = 0.5;

            while (t < EndTime - end_epsilon)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tick = new HoldNoteTick
                {
                    StartTime = t,
                    Column = Column,
                };
                Ticks.Add(tick);
                AddNested(tick);
                t += interval;
            }

            // 短于一格的 LN：在尾前放一格，保证按住仍有 combo tick。
            if (Ticks.Count == 0 && Duration > end_epsilon)
            {
                var tick = new HoldNoteTick
                {
                    StartTime = EndTime - end_epsilon,
                    Column = Column,
                };
                Ticks.Add(tick);
                AddNested(tick);
            }
        }

        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public IList<HitSampleInfo> GetNodeSamples(int nodeIndex) => nodeIndex < NodeSamples?.Count ? NodeSamples[nodeIndex] : Samples;

        /// <summary>
        /// Create the default note samples for a hold note, based off their main sample.
        /// </summary>
        /// <remarks>
        /// By default, osu!mania beatmaps in only play samples at the start of the hold note.
        /// </remarks>
        /// <param name="obj">The object to use as a basis for the head sample.</param>
        /// <returns>Defaults for assigning to <see cref="HoldNote.NodeSamples"/>.</returns>
        public static List<IList<HitSampleInfo>> CreateDefaultNodeSamples(HitObject obj) => new List<IList<HitSampleInfo>>
        {
            obj.Samples,
            new List<HitSampleInfo>(),
        };
    }
}
