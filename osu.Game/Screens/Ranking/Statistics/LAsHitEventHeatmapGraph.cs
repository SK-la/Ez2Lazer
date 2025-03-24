using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class LAsHitEventHeatmapGraph : CompositeDrawable
    {
        private const int time_bins = 50;
        private readonly IReadOnlyList<HitEvent> hitEvents;
        private readonly IDictionary<HitResult, int>[] bins;
        private double binSize;
        private double hitOffset;

        private Dot[]? dotDrawables;

        public LAsHitEventHeatmapGraph(IReadOnlyList<HitEvent> hitEvents)
        {
            this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic() && e.Result.IsHit()).ToList();
            bins = Enumerable.Range(0, time_bins).Select(_ => new Dictionary<HitResult, int>()).ToArray<IDictionary<HitResult, int>>();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEvents.Count == 0)
                return;

            binSize = Math.Ceiling(hitEvents.Max(e => e.HitObject.StartTime) / time_bins);

            binSize = Math.Max(1, binSize);

            Scheduler.AddOnce(updateDisplay);
        }

        public void UpdateOffset(double hitOffset)
        {
            this.hitOffset = hitOffset;
            Scheduler.AddOnce(updateDisplay);
        }

        private void updateDisplay()
        {
            foreach (var bin in bins)
                bin.Clear();

            foreach (var e in hitEvents)
            {
                double time = e.HitObject.StartTime + hitOffset;
                int index = (int)(time / binSize);

                if (index >= 0 && index < bins.Length)
                {
                    bins[index].TryGetValue(e.Result, out int value);
                    bins[index][e.Result] = ++value;
                }
            }

            if (dotDrawables == null)
                createDotDrawables();
            else
            {
                for (int i = 0; i < dotDrawables.Length; i++)
                    dotDrawables[i].UpdateOffset(bins[i].Sum(b => b.Value));
            }
        }

        private void createDotDrawables()
        {
            int maxCount = bins.Max(b => b.Values.Sum());
            dotDrawables = bins.Select((_, i) => new Dot(bins[i], maxCount)).ToArray();

            Container axisFlow;

            Padding = new MarginPadding { Horizontal = 5 };

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Content = new[]
                {
                    new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Content = new[] { dotDrawables }
                        }
                    },
                    new Drawable[]
                    {
                        axisFlow = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = StatisticItem.FONT_SIZE,
                        }
                    },
                },
                RowDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                }
            };

            double maxValue = time_bins * binSize;
            double axisValueStep = maxValue / 5;

            for (int i = -5; i <= 5; i++)
            {
                double axisValue = i * axisValueStep;
                float position = maxValue == 0 ? 0 : (float)(axisValue / maxValue);
                float alpha = 1f - Math.Abs(position) * 0.8f;

                TimeSpan time = TimeSpan.FromMilliseconds(axisValue);
                string timeText = $"{(i < 0 ? "-" : "+")}{time.Minutes:D2}:{time.Seconds:D2}";

                axisFlow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativePositionAxes = Axes.X,
                    X = position / 2,
                    Alpha = alpha,
                    Text = timeText,
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold)
                });
            }
        }

        private partial class Dot : CompositeDrawable
        {
            private readonly IReadOnlyList<KeyValuePair<HitResult, int>> values;
            private readonly float maxValue;
            private readonly float totalValue;

            private const float minimum_height = 0.02f;

            private float offsetAdjustment;

            private Circle[] dotOriginals = null!;

            private Circle? dotAdjustment;

            private float? lastDrawHeight;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            private const double duration = 300;

            public Dot(IDictionary<HitResult, int> values, float maxValue)
            {
                this.values = values.OrderBy(v => v.Key.GetIndexForOrderedDisplay()).ToList();
                this.maxValue = maxValue;
                totalValue = values.Sum(v => v.Value);
                offsetAdjustment = totalValue;

                RelativeSizeAxes = Axes.Both;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (values.Any())
                {
                    dotOriginals = values.Select((v, i) => new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = colours.ForHitResult(v.Key),
                        Height = 0,
                    }).ToArray();
                    InternalChildren = dotOriginals.Reverse().ToArray();
                }
                else
                {
                    InternalChildren = dotOriginals = new[]
                    {
                        new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = Color4.Gray,
                            Height = 0,
                        }
                    };
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Scheduler.AddOnce(updateMetrics, true);
            }

            protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
            {
                if (invalidation.HasFlag(Invalidation.DrawSize))
                {
                    if (lastDrawHeight != null && lastDrawHeight != DrawHeight)
                        Scheduler.AddOnce(updateMetrics, false);
                }

                return base.OnInvalidate(invalidation, source);
            }

            public void UpdateOffset(float adjustment)
            {
                bool hasAdjustment = adjustment != totalValue;

                if (dotAdjustment == null)
                {
                    if (!hasAdjustment)
                        return;

                    AddInternal(dotAdjustment = new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Color4.Yellow,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0.6f,
                        Height = 0,
                    });
                }

                offsetAdjustment = adjustment;

                Scheduler.AddOnce(updateMetrics, true);
            }

            private void updateMetrics(bool animate = true)
            {
                float offsetValue = 0;

                for (int i = 0; i < dotOriginals.Length; i++)
                {
                    int value = i < values.Count ? values[i].Value : 0;

                    var dot = dotOriginals[i];

                    dot.MoveToY(offsetForValue(offsetValue) * BoundingBox.Height, duration, Easing.OutQuint);
                    dot.ResizeHeightTo(heightForValue(value), duration, Easing.OutQuint);
                    offsetValue -= value;
                }

                if (dotAdjustment != null)
                    drawAdjustmentDot();

                if (!animate)
                    FinishTransforms(true);

                lastDrawHeight = DrawHeight;
            }

            private void drawAdjustmentDot()
            {
                bool hasAdjustment = offsetAdjustment != totalValue;

                dotAdjustment.ResizeHeightTo(heightForValue(offsetAdjustment), duration, Easing.OutQuint);
                dotAdjustment.FadeTo(!hasAdjustment ? 0 : 1, duration, Easing.OutQuint);
            }

            private float offsetForValue(float value) => maxValue == 0 ? 0 : (1 - minimum_height) * value / maxValue;

            private float heightForValue(float value) => minimum_height + offsetForValue(value);
        }
    }
}
