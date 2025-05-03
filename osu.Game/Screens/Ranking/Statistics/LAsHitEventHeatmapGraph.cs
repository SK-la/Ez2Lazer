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
    public partial class LAsHitEventHeatmapGraph : CompositeDrawable
    {
        private const int time_bins = 50; // 时间分段数
        private const float circle_size = 5f; // 圆形大小
        private readonly IReadOnlyList<HitEvent> hitEvents;
        private double binSize;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly HitWindows hitWindows;

        public LAsHitEventHeatmapGraph(IReadOnlyList<HitEvent> hitEvents, HitWindows hitWindows)
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

            // var allAvailableWindows = hitWindows.GetAllAvailableWindows();

            // 遍历所有有效的 HitResult，绘制边界线
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

            const float left_margin = 45; // 左侧预留空间
            const float right_margin = 50; // 右侧预留空间

            // 绘制每个 HitEvent 的圆点
            foreach (var e in hitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = (float)(time / (time_bins * binSize)); // 计算 x 轴位置
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
            // // 计算当前区间内的 note 数量占比
            // int notesInBoundary = hitEvents.Count(e => e.TimeOffset <= boundary);
            // float noteRatio = (float)notesInBoundary / hitEvents.Count;
            //
            // // 根据 noteRatio 动态调整透明度，noteRatio 越大透明度越低
            // float adjustedAlpha = 0.1f + (1 - noteRatio) * 0.3f; // 最低透明度为 0.2f，最高为 0.5f
            const float margin = 30;
            // 绘制中心轴 (0ms)
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
