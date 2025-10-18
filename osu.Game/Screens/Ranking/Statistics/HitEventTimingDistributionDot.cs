using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class HitEventTimingDistributionDot : CompositeDrawable
    {
        private const int time_bins = 50;

        private const float circle_size = 5f;

        private readonly IReadOnlyList<HitEvent> hitEvents;

        private double binSize;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly HitWindows hitWindows;

        public HitEventTimingDistributionDot(IReadOnlyList<HitEvent> hitEvents, HitWindows hitWindows)
        {
            this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic() && e.Result.IsHit()).ToList();
            this.hitWindows = hitWindows;
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

        private void updateDisplay()
        {
            ClearInternal();

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>())
            {
                if (!result.IsBasic() || !result.IsHit())
                    continue;

                double boundary = hitWindows.WindowFor(result);

                if (boundary <= 0)
                    continue;

                drawBoundaryLine(boundary, result);
                drawBoundaryLine(-boundary, result);
            }

            const float left_margin = 45;
            const float right_margin = 50;

            foreach (var e in hitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = (float)(time / (time_bins * binSize));
                float yPosition = (float)(e.TimeOffset);

                AddInternal(new Circle
                {
                    Size = new Vector2(circle_size),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    X = (xPosition * (DrawWidth - left_margin - right_margin)) - (DrawWidth / 2) + left_margin,
                    Y = yPosition,
                    Alpha = 0.8f,
                    Colour = colours.ForHitResult(e.Result),
                });
            }
        }

        private void drawBoundaryLine(double boundary, HitResult result)
        {
            const float margin = 30;

            AddInternal(new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Width = 1 - (2 * margin / DrawWidth),
                Alpha = 0.1f,
                Colour = Color4.Gray,
            });

            AddInternal(new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Width = 1 - (2 * margin / DrawWidth),
                Alpha = 0.1f,
                Colour = colours.ForHitResult(result),
                Y = (float)(boundary),
            });

            AddInternal(new OsuSpriteText
            {
                Text = $"{boundary:+0.##;-0.##}",
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                Font = OsuFont.GetFont(size: 14),
                Colour = Color4.White,
                X = 25,
                Y = (float)(boundary),
            });
        }
    }
}
