// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    /// <summary>
    /// 密集下落标定用的真实 drawable 字段：列 + 滚动容器 + 按驻留窗口流式生成 note。
    /// <para>
    /// 不自带时钟；调用方传入 <see cref="FramedClock"/> 并负责推进，因此交互场景可以自由暂停。
    /// </para>
    /// </summary>
    public partial class ManiaDenseScrollField : Container
    {
        public ManiaDenseScrollWorkload Workload { get; }

        public IReadOnlyList<Column> Columns => columns;

        public long SimulatedFrames { get; private set; }

        public int AliveNoteCount
        {
            get
            {
                int count = 0;

                foreach (var column in columns)
                    count += column.HitObjectContainer.AliveObjects.Count();

                return count;
            }
        }

        /// <summary>被动 Miss 的窗口：note 在滚动窗口结束后还要留到这时才会被移除。</summary>
        public double PassiveMissTailMs
        {
            get
            {
                var note = this.ChildrenOfType<DrawableNote>().FirstOrDefault();
                return note?.HitObject.HitWindows?.WindowFor(HitResult.Miss) ?? 0;
            }
        }

        private readonly List<Column> columns = new List<Column>();
        private readonly double[] nextStart;
        private readonly ManiaBeatmap beatmap;

        public ManiaDenseScrollField(ManiaDenseScrollWorkload workload, ISkin skin, FramedClock clock)
        {
            Workload = workload;
            Workload.Validate();

            beatmap = new ManiaBeatmap(new StageDefinition(workload.ColumnCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty(),
                },
            };

            Clock = clock;

            nextStart = new double[workload.ColumnCount];

            var columnFlow = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
            };

            var transformed = new ManiaRuleset().CreateSkinTransformer(skin, beatmap) ?? skin;

            AddInternal(new SkinProvidingContainer(transformed)
            {
                RelativeSizeAxes = Axes.Both,
                Child = columnFlow,
            });

            for (int i = 0; i < workload.ColumnCount; i++)
            {
                var column = new Column(i, false)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Height = 0.85f,
                };

                columns.Add(column);

                columnFlow.Add(new ScrollingTestContainer(ScrollingDirection.Down)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    TimeRange = workload.DwellMs,
                    Child = column,
                });

                nextStart[i] = workload.GetNextStartTime(i, 0);
            }
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(new StageDefinition(Workload.ColumnCount));
            // Ez2 / EzPro 的 key area 和 hit target 需要 IGameplayClock 做 beat phase 推进。
            dependencies.CacheAs<IGameplayClock>(new SimulatedGameplayClock((IFrameBasedClock)Clock));
            return dependencies;
        }

        protected override void Update()
        {
            base.Update();

            SimulatedFrames++;

            double now = Time.Current;
            double limit = now + Workload.DwellMs + Workload.IntervalMs;

            for (int column = 0; column < columns.Count; column++)
            {
                double start = nextStart[column];

                while (start <= limit)
                {
                    spawn(column, start);
                    start += Workload.IntervalMs;
                }

                nextStart[column] = start;
            }
        }

        private void spawn(int column, double startTimeMs)
        {
            if (Workload.IsHoldAt(column, startTimeMs))
                columns[column].Add(Workload.CreateHoldNote(column, startTimeMs));
            else
                columns[column].Add(Workload.CreateNote(column, startTimeMs));
        }

        /// <summary>
        /// 只提供时间推进的 <see cref="IGameplayClock"/>：皮肤用它推进 beat phase，不参与判定。
        /// </summary>
        private class SimulatedGameplayClock : IGameplayClock
        {
            private readonly IFrameBasedClock clock;
            private readonly AudioAdjustments adjustments = new AudioAdjustments();
            private readonly BindableBool isPaused = new BindableBool();

            public SimulatedGameplayClock(IFrameBasedClock clock)
            {
                this.clock = clock;
            }

            public double CurrentTime => clock.CurrentTime;
            public double Rate => clock.Rate;
            public bool IsRunning => clock.IsRunning;
            public bool IsRewinding => false;
            public double ElapsedFrameTime => clock.ElapsedFrameTime;
            public double FramesPerSecond => clock.FramesPerSecond;
            public FrameTimeInfo TimeInfo => clock.TimeInfo;
            public double StartTime => 0;
            public double GameplayStartTime => 0;
            public IBindable<bool> IsPaused => isPaused;
            public IEnumerable<double> NonGameplayAdjustments => Array.Empty<double>();
            public IAdjustableAudioComponent AdjustmentsFromMods => adjustments;

            public double TrueGameplayRate
            {
                set => adjustments.Tempo.Value = value;
            }

            public void ProcessFrame()
            {
            }
        }
    }
}
