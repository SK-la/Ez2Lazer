// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

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
    public class Slider : OsuHitObject, IHasPathWithRepeats, IHasSliderVelocity, IHasGenerateTicks
    {
        public double EndTime => StartTime + getTotalDuration();

        [JsonIgnore]
        public double Duration
        {
            get => EndTime - StartTime;
            set => throw new System.NotSupportedException($"Adjust via {nameof(RepeatCount)} instead"); // can be implemented if/when needed.
        }

        public override IList<HitSampleInfo> AuxiliarySamples => CreateSlidingSamples().Concat(TailSamples).ToArray();

        private readonly Cached<Vector2> endPositionCache = new Cached<Vector2>();

        public override Vector2 EndPosition => endPositionCache.IsValid
            ? endPositionCache.Value
            : endPositionCache.Value = isDurationCollapsed ? Position : Position + this.CurvePositionAt(1);

        public Vector2 StackedPositionAt(double t) => StackedPosition + this.CurvePositionAt(t);

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

        private double? endTimeLimit;

        /// <summary>
        /// The latest end time this slider may have before its duration is treated as unrepresentable and collapses to zero.
        /// <see langword="null"/> keeps the literal duration.
        /// </summary>
        /// <remarks>
        /// Aspire maps carry slider lengths that put the end time days or years beyond the map itself. osu!stable stores
        /// end times as 32-bit integers, so such lengths yield no usable end time there and the sliders effectively do
        /// not exist. Reproducing that here also keeps an object from staying alive for an unbounded time while forcing
        /// a path approximation over hundreds of thousands of control points.
        /// </remarks>
        public double? EndTimeLimit
        {
            get => endTimeLimit;
            set
            {
                if (endTimeLimit == value)
                    return;

                endTimeLimit = value;
                endPositionCache.Invalidate();
                updateNestedPositions();
            }
        }

        private bool isDurationCollapsed => endTimeLimit != null && getTotalDuration() <= 0;

        /// <summary>
        /// The duration of all spans.
        /// </summary>
        /// <remarks>
        /// Without <see cref="EndTimeLimit"/> this is exactly the legacy expression, whatever the resulting value.
        /// </remarks>
        private double getTotalDuration()
        {
            int spanCount = this.SpanCount();

            if (endTimeLimit is not double limit || Velocity <= 0)
                return spanCount * Path.Distance / Velocity;

            // Check the author-set length first: it bounds the real distance and costs nothing, whereas Path.Distance
            // forces a full approximation of a path that may hold hundreds of thousands of control points.
            if (Path.ExpectedDistance.Value is double expectedDistance && !isRepresentableDuration(expectedDistance))
                return 0;

            double distance = Path.Distance;

            return isRepresentableDuration(distance) ? spanCount * distance / Velocity : 0;

            bool isRepresentableDuration(double distance)
            {
                double duration = spanCount * distance / Velocity;

                return double.IsFinite(duration) && duration >= 0 && StartTime + duration <= limit;
            }
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

            // A slider whose duration collapsed (see getTotalDuration) has no time for ticks or repeats, and its path
            // is exactly the shape we must avoid approximating. Only the head and tail exist, both at the start time.
            var sliderEvents = spanDuration > 0
                ? SliderEventGenerator.Generate(StartTime, spanDuration, Velocity, TickDistance, Path.Distance, this.SpanCount(), cancellationToken)
                : CreateDegenerateEvents();

            foreach (var e in sliderEvents)
            {
                switch (e.Type)
                {
                    case SliderEventType.Tick:
                        AddNested(new SliderTick
                        {
                            SpanIndex = e.SpanIndex,
                            SpanStartTime = e.SpanStartTime,
                            StartTime = e.Time,
                            Position = Position + Path.PositionAt(e.PathProgress),
                            PathProgress = e.PathProgress,
                            StackHeight = StackHeight,
                        });
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
                            // Reading EndPosition would approximate the entire path, so a collapsed slider stays where it starts.
                            Position = spanDuration > 0 ? EndPosition : Position,
                            StackHeight = StackHeight,
                            ClassicSliderBehaviour = ClassicSliderBehaviour,
                        });
                        break;

                    case SliderEventType.Repeat:
                        AddNested(LastRepeat = new SliderRepeat(this)
                        {
                            RepeatIndex = e.SpanIndex,
                            StartTime = StartTime + (e.SpanIndex + 1) * spanDuration,
                            Position = Position + Path.PositionAt(e.PathProgress),
                            StackHeight = StackHeight,
                            PathProgress = e.PathProgress,
                        });
                        break;
                }
            }

            UpdateNestedSamples();
        }

        /// <summary>
        /// The only events a collapsed slider produces: a head and a tail, both at the slider's start time.
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
