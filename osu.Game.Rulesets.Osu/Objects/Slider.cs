// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class Slider : OsuHitObject, IHasPathWithRepeats, IHasSliderVelocity, IHasGenerateTicks, IExcludeEndTimeFromPlayableLength
    {
        public double EndTime => StartTime + getTotalDuration();

        [JsonIgnore]
        public double Duration
        {
            get => EndTime - StartTime;
            set => throw new NotSupportedException($"Adjust via {nameof(RepeatCount)} instead"); // can be implemented if/when needed.
        }

        public override IList<HitSampleInfo> AuxiliarySamples => CreateSlidingSamples().Concat(TailSamples).ToArray();

        private readonly Cached<Vector2> endPositionCache = new Cached<Vector2>();

        public override Vector2 EndPosition => endPositionCache.IsValid
            ? endPositionCache.Value
            : endPositionCache.Value = Position + this.CurvePositionAt(1);

        public Vector2 StackedPositionAt(double t) => StackedPosition + this.CurvePositionAt(t);

        /// <summary>
        /// 球是否按 osu!stable 的映射放置，而非沿绘制曲线均匀行走。
        /// </summary>
        /// <remarks>
        /// osu!stable 用作者设定长度缩放进度得到沿曲线距离，再钳到实际绘制的曲线。
        /// 声明长于绘制时，球会提早走完曲线并在终点停留至滑条结束，读起来像慢漂后突然到位。
        /// 只影响球、尾与折返的放置；判定仍用自己的规则。
        /// </remarks>
        public bool UseStableBallPosition { get; set; }

        /// <summary>
        /// 谱面为该滑条声明的长度，独立于 <see cref="SliderPath.ExpectedDistance"/> 保留。
        /// </summary>
        /// <remarks>
        /// 路径必须截到绘制曲线，使身跟随几何而非追加直线，这会覆盖 <see cref="SliderPath.ExpectedDistance"/>。
        /// stable 在旁保留声明长度并用于球，因此调整路径前必须记住原值。
        /// </remarks>
        public double? StableDeclaredDistance { get; set; }

        /// <summary>
        /// 该滑条在 <paramref name="progress"/> 处的球位置，遵循 <see cref="UseStableBallPosition"/>。
        /// </summary>
        public Vector2 BallPositionAt(double progress)
        {
            if (!UseStableBallPosition)
                return this.CurvePositionAt(progress);

            double drawn = Path.Distance;

            if (drawn <= 0)
                return Path.PositionAt(0);

            // stable 用谱面声明长度沿曲线量距，而非控制点描出的长度，并钳到绘制曲线。
            // 声明长于绘制时球提早走完并在终点停留余下时间。
            double declared = StableDeclaredDistance ?? Path.ExpectedDistance.Value ?? drawn;
            double spanProgress = this.ProgressAt(progress);

            return Path.PositionAt(Math.Clamp(declared * spanProgress / drawn, 0, 1));
        }

        private readonly SliderPath path = new SliderPath { OptimiseCatmull = true };

        public SliderPath Path
        {
            get => path;
            set => path.CopyFrom(value);
        }

        public double Distance => Path.Distance;

        public override Vector2 Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                updateNestedPositions();
            }
        }

        public IList<IList<HitSampleInfo>> NodeSamples { get; set; } = new List<IList<HitSampleInfo>>();

        [JsonIgnore]
        public IList<HitSampleInfo> TailSamples { get; private set; }

        private int repeatCount;

        public int RepeatCount
        {
            get => repeatCount;
            set
            {
                repeatCount = value;
                updateNestedPositions();
            }
        }

        /// <summary>
        /// The length of one span of this <see cref="Slider"/>.
        /// </summary>
        public double SpanDuration => Duration / this.SpanCount();

        private bool limitDurationToGeometry;

        /// <summary>
        /// 滑条身是否保持完整绘制，而不随球经过而收缩。
        /// </summary>
        /// <remarks>
        /// osu!stable 把身生长一次后不再改动，结束时淡出消失。沿球收缩是 lazer 独有效果，
        /// 也是极密路径在整段滑条每帧重建顶点列表的开销来源。
        /// </remarks>
        public bool SuppressSnakingOut { get; set; }

        /// <summary>
        /// 本滑条结束时间是否不计入谱面可玩区域。
        /// </summary>
        /// <remarks>
        /// osu!stable 用 hitobject 开始时间量谱面长度，仅转盘计结束时间。
        /// 声明长度膨胀的滑条在那里不会拉长谱面；若在此取结束时间会把图末推迟数天。
        /// </remarks>
        public bool ExcludeEndTimeFromPlayableLength { get; set; }

        /// <summary>
        /// 与头/尾重叠的 tick 与折返是否像 osu!stable 一样不绘制。
        /// </summary>
        /// <remarks>
        /// stable 仅在评分点距两端至少一个物件半径时才绘制，但仍判定计分。
        /// Aspire 把数千折返叠进不足一单位，几乎每个都与两端重叠，stable 因此一个都不画。
        /// </remarks>
        public bool HideOverlappingScoringPoints { get; set; }

        /// <summary>
        /// osu!stable 揭示该滑条身的窗口结束时刻。
        /// </summary>
        /// <remarks>
        /// stable 把累计结束时间存成 int（<c>EndTime = (int)accumulated</c>）。超出 int 范围的 double 变成
        /// <see cref="int.MinValue"/>，窗口已关闭，身永不显示。若只保留低 32 位可能落到未来并让身一直存活。
        /// 计分仍用 <see cref="EndTime"/> 与真实路径长度；只改此窗口。
        /// </remarks>
        public double StableRevealEndTime
        {
            get
            {
                if (!limitDurationToGeometry || Velocity <= 0)
                    return EndTime;

                double accumulatedEnd = StartTime + Duration;

                // 仍能装下的值向零截断并留在真实结束时间。
                // 达到或超过 2^31 的变成 int.MinValue，与 (int)double 越界行为一致。
                if (accumulatedEnd < (double)int.MaxValue + 1)
                    return EndTime;

                return int.MinValue;
            }
        }

        /// <summary>
        /// stable 的 int 截断是否在身窗打开前就把它关掉。
        /// </summary>
        public bool RevealEndTimeWrapsUnderStableRules => StableRevealEndTime <= StartTime;

        /// <summary>
        /// 时长是否对齐 osu!stable：作者设定长度只能截短路经。
        /// </summary>
        /// <remarks>
        /// <para>
        /// osu!stable 累加真实线段长度求结束时间，作者设定长度只用于截短，从不延伸。
        /// 此处 <see cref="SliderPath.Distance"/> 是延伸后长度，因此几何只有一点半、声明却巨大的 aspire 滑条
        /// 会持续数天，而非 stable 给出的毫秒。
        /// </para>
        /// <para>
        /// 无此标志时时长就是遗留表达式，不论结果多大。
        /// </para>
        /// </remarks>
        public bool LimitDurationToGeometry
        {
            get => limitDurationToGeometry;
            set
            {
                if (limitDurationToGeometry == value)
                    return;

                limitDurationToGeometry = value;
                endPositionCache.Invalidate();
                updateNestedPositions();
            }
        }

        /// <summary>
        /// 一个 span 走过的距离：绘制路径与作者设定长度中较短者。
        /// </summary>
        /// <remarks>
        /// 仅当作者设定长度延伸路径时与 <see cref="SliderPath.Distance"/> 不同；osu!stable 从不延伸。
        /// </remarks>
        private double getEffectiveDistance()
            => limitDurationToGeometry ? Math.Min(Path.Distance, Path.CalculatedDistance) : Path.Distance;

        /// <summary>
        /// 路径末端停止生成 tick 的距离。
        /// </summary>
        /// <remarks>
        /// osu!stable 在剩余 span 长度降到 <c>0.01 * velocity</c> 时停止（<c>SliderOsu.cs:942, 985</c>），
        /// 共用生成器停在 <c>velocity * 10</c>。几何不足一单位的 aspire 滑条两者差三个数量级：
        /// stable 完全不产生评分点，默认却会。
        /// </remarks>
        private double getTickEndMargin()
            => limitDurationToGeometry ? Velocity * 0.01 : Velocity * 10;

        /// <summary>
        /// 全部 span 的时长。
        /// </summary>
        private double getTotalDuration()
        {
            int spanCount = this.SpanCount();

            if (!limitDurationToGeometry || Velocity <= 0)
                return spanCount * Path.Distance / Velocity;

            return spanCount * getEffectiveDistance() / Velocity;
        }

        /// <summary>
        /// The computed velocity of this <see cref="Slider"/>. This is the amount of path distance travelled in 1 ms.
        /// </summary>
        public double Velocity { get; private set; }

        /// <summary>
        /// Spacing between <see cref="SliderTick"/>s of this <see cref="Slider"/>.
        /// </summary>
        public double TickDistance { get; private set; }

        /// <summary>
        /// An extra multiplier that affects the number of <see cref="SliderTick"/>s generated by this <see cref="Slider"/>.
        /// An increase in this value increases <see cref="TickDistance"/>, which reduces the number of ticks generated.
        /// </summary>
        public double TickDistanceMultiplier = 1;

        /// <summary>
        /// If <see langword="false"/>, <see cref="Slider"/>'s judgement is fully handled by its nested <see cref="HitObject"/>s.
        /// If <see langword="true"/>, this <see cref="Slider"/> will be judged proportionally to the number of nested <see cref="HitObject"/>s hit.
        /// </summary>
        public bool ClassicSliderBehaviour
        {
            get => classicSliderBehaviour;
            set
            {
                classicSliderBehaviour = value;
                HeadCircle?.ClassicSliderBehaviour = value;
                TailCircle?.ClassicSliderBehaviour = value;
            }
        }

        private bool classicSliderBehaviour;

        public BindableNumber<double> SliderVelocityMultiplierBindable { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 10
        };

        public double SliderVelocityMultiplier
        {
            get => SliderVelocityMultiplierBindable.Value;
            set => SliderVelocityMultiplierBindable.Value = value;
        }

        public bool GenerateTicks { get; set; } = true;

        [JsonIgnore]
        public SliderHeadCircle HeadCircle { get; protected set; }

        [JsonIgnore]
        public SliderTailCircle TailCircle { get; protected set; }

        [JsonIgnore]
        [CanBeNull]
        public SliderRepeat LastRepeat { get; protected set; }

        /// <summary>
        /// 本滑条最后一个 <see cref="SliderTick"/> 或 <see cref="SliderRepeat"/>，或 <see langword="null"/>。
        /// </summary>
        /// <remarks>
        /// 尾只能在它结算后判定，因此判定路径每帧对每个嵌套物件都需要它。缓存使查找为常数，而非扫描整份嵌套列表。
        /// </remarks>
        [JsonIgnore]
        [CanBeNull]
        public HitObject LastScoredNested { get; protected set; }

        public Slider()
        {
            SamplesBindable.CollectionChanged += (_, _) => UpdateNestedSamples();
            Path.Version.ValueChanged += _ => updateNestedPositions();
        }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            TimingControlPoint timingPoint = controlPointInfo.TimingPointAt(StartTime);

            Velocity = BASE_SCORING_DISTANCE * difficulty.SliderMultiplier / LegacyRulesetExtensions.GetPrecisionAdjustedBeatLength(this, timingPoint, OsuRuleset.SHORT_NAME);
            // WARNING: this is intentionally not computed as `BASE_SCORING_DISTANCE * difficulty.SliderMultiplier`
            // for backwards compatibility reasons (intentionally introducing floating point errors to match stable).
            double scoringDistance = Velocity * timingPoint.BeatLength;

            TickDistance = GenerateTicks ? (scoringDistance / difficulty.SliderTickRate * TickDistanceMultiplier) : double.PositiveInfinity;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            double spanDuration = SpanDuration;

            // 时长塌缩的滑条没有 tick/折返的时间，其路径正是必须避免近似的形状。只存在头和尾，都在开始时刻。
            var sliderEvents = spanDuration > 0
                ? SliderEventGenerator.Generate(StartTime, spanDuration, Velocity, TickDistance, getEffectiveDistance(), this.SpanCount(),
                                                getTickEndMargin(), cancellationToken, skipRepeatsOnShortSpans: limitDurationToGeometry)
                : CreateDegenerateEvents();

            // 添加时跟踪，而非从 NestedHitObjects 回读：此处建好的嵌套物件进构造列表，本方法返回后才替换 NestedHitObjects。
            HitObject? lastScoredNested = null;

            // stable 跳过与任一端重叠的评分点绘制；整条滑条都那么小时整条跳过。仅在按 stable 规则游玩时应用。
            double radiusSquared = Radius * Radius;
            Vector2 headPosition = Position;
            Vector2 tailPosition = spanDuration > 0 ? EndPosition : Position;

            bool overlapsEnds(Vector2 point) => HideOverlappingScoringPoints
                                               && (Vector2.DistanceSquared(point, headPosition) < radiusSquared
                                                   || Vector2.DistanceSquared(point, tailPosition) < radiusSquared);

            foreach (var e in sliderEvents)
            {
                switch (e.Type)
                {
                    case SliderEventType.Tick:
                        lastScoredNested = new SliderTick
                        {
                            SpanIndex = e.SpanIndex,
                            SpanStartTime = e.SpanStartTime,
                            StartTime = e.Time,
                            Position = Position + Path.PositionAt(e.PathProgress),
                            PathProgress = e.PathProgress,
                            StackHeight = StackHeight,
                            UndrawnUnderStableRules = overlapsEnds(Position + Path.PositionAt(e.PathProgress)),
                        };

                        AddNested(lastScoredNested);
                        break;

                    case SliderEventType.Head:
                        AddNested(HeadCircle = new SliderHeadCircle
                        {
                            StartTime = e.Time,
                            Position = Position,
                            StackHeight = StackHeight,
                            ClassicSliderBehaviour = ClassicSliderBehaviour,
                        });
                        break;

                    case SliderEventType.Tail:
                        AddNested(TailCircle = new SliderTailCircle(this)
                        {
                            RepeatIndex = e.SpanIndex,
                            StartTime = e.Time,
                            // 读 EndPosition 会近似整条路径，因此塌缩滑条留在起点。
                            Position = spanDuration > 0 ? EndPosition : Position,
                            StackHeight = StackHeight,
                            ClassicSliderBehaviour = ClassicSliderBehaviour,
                        });
                        break;

                    case SliderEventType.Repeat:
                        lastScoredNested = new SliderRepeat(this)
                        {
                            RepeatIndex = e.SpanIndex,
                            StartTime = StartTime + (e.SpanIndex + 1) * spanDuration,
                            Position = Position + Path.PositionAt(e.PathProgress),
                            StackHeight = StackHeight,
                            PathProgress = e.PathProgress,
                            UndrawnUnderStableRules = overlapsEnds(Position + Path.PositionAt(e.PathProgress)),
                        };

                        AddNested(LastRepeat = (SliderRepeat)lastScoredNested);
                        break;
                }
            }

            LastScoredNested = lastScoredNested;

            UpdateNestedSamples();
        }

        /// <summary>
        /// 塌缩滑条仅产生的事件：头与尾，都在滑条开始时刻。
        /// </summary>
        protected IEnumerable<SliderEventDescriptor> CreateDegenerateEvents()
        {
            yield return new SliderEventDescriptor
            {
                Type = SliderEventType.Head,
                Time = StartTime,
                SpanStartTime = StartTime,
            };

            yield return new SliderEventDescriptor
            {
                Type = SliderEventType.Tail,
                Time = StartTime,
                SpanStartTime = StartTime,
                SpanIndex = this.SpanCount() - 1,
            };
        }

        private void updateNestedPositions()
        {
            endPositionCache.Invalidate();

            foreach (var nested in NestedHitObjects)
            {
                switch (nested)
                {
                    case SliderHeadCircle headCircle:
                        headCircle.Position = Position;
                        break;

                    case SliderTailCircle tailCircle:
                        tailCircle.Position = EndPosition;
                        break;

                    case SliderRepeat repeat:
                        repeat.Position = Position + Path.PositionAt(repeat.PathProgress);
                        break;

                    case SliderTick tick:
                        tick.Position = Position + Path.PositionAt(tick.PathProgress);
                        break;
                }
            }
        }

        protected void UpdateNestedSamples()
        {
            this.PopulateNodeSamples();

            // TODO: remove this when guaranteed sort is present for samples (https://github.com/ppy/osu/issues/1933)
            HitSampleInfo tickSample = (Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL) ?? Samples.FirstOrDefault())?.With("slidertick");

            foreach (var nested in NestedHitObjects)
            {
                switch (nested)
                {
                    case SliderTick tick:
                        tick.SamplesBindable.Clear();

                        if (tickSample != null)
                            tick.SamplesBindable.Add(tickSample);
                        break;

                    case SliderRepeat repeat:
                        repeat.Samples = this.GetNodeSamples(repeat.RepeatIndex + 1);
                        break;
                }
            }

            HeadCircle?.Samples = this.GetNodeSamples(0);

            // The samples should be attached to the slider tail, however this can only be done if LastTick is removed otherwise they would play earlier than they're intended to.
            // (see mapping logic in `CreateNestedHitObjects` above)
            //
            // For now, the samples are played by the slider itself at the correct end time.
            TailSamples = this.GetNodeSamples(repeatCount + 1);
        }

        public override Judgement CreateJudgement() => ClassicSliderBehaviour
            // Final combo is provided by the slider itself - see logic in `DrawableSlider.CheckForResult()`
            ? new OsuJudgement()
            // Final combo is provided by the tail circle - see `SliderTailCircle`
            : new OsuIgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
