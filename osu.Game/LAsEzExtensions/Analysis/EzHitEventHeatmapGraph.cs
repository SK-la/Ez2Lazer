using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Lines;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public partial class EzHitEventHeatmapGraph : CompositeDrawable
    {
        private const int time_bins = 50; // 时间分段数
        private const float circle_size = 5f; // 圆形大小
        private readonly IReadOnlyList<HitEvent> hitEvents;
        private double binSize;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly HitWindows hitWindows;

        public EzHitEventHeatmapGraph(IReadOnlyList<HitEvent> hitEvents, HitWindows hitWindows)
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

            // 计算并绘制血量折线
            // drawHealthLine(left_margin, right_margin);
        }

        private void drawHealthLine(float left_margin, float right_margin)
        {
            var sortedEvents = hitEvents.OrderBy(e => e.HitObject.StartTime).ToList();
            double currentHealth = 1; // 初始血量
            List<Vector2> healthPoints = new List<Vector2>();

            foreach (var e in sortedEvents)
            {
                var judgement = e.HitObject.CreateJudgement();
                var judgementResult = new JudgementResult(e.HitObject, judgement) { Type = e.Result };
                double healthIncrease = judgement.HealthIncreaseFor(judgementResult);
                currentHealth = Math.Clamp(currentHealth + healthIncrease, 0, 1);

                double time = e.HitObject.StartTime;
                float xPosition = (float)(time / (time_bins * binSize));
                float x = (xPosition * (DrawWidth - left_margin - right_margin)) - (DrawWidth / 2) + left_margin;
                float y = (float)((1 - currentHealth) * DrawHeight - DrawHeight / 2);

                healthPoints.Add(new Vector2(x, y));
            }

            if (healthPoints.Count > 1)
            {
                AddInternal(new Path
                {
                    PathRadius = 1,
                    Colour = Color4.Red,
                    Alpha = 0.5f,
                    Vertices = healthPoints.ToArray()
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

    public partial class CreateRotatedColumnGraphs : CompositeDrawable
    {
        private const float horizontal_spacing_ratio = 0.015f;
        private const float top_margin = 20;
        private const float bottom_margin = 10;
        private const float horizontal_margin = 10;

        private readonly List<IGrouping<int, HitEvent>> hitEventsByColumn;

        public CreateRotatedColumnGraphs(List<IGrouping<int, HitEvent>> hitEventsByColumn)
        {
            this.hitEventsByColumn = hitEventsByColumn;
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEventsByColumn.Count == 0)
                return;

            // 创建所有UI元素
            for (int i = 0; i < hitEventsByColumn.Count; i++)
            {
                var column = hitEventsByColumn[i];

                // 添加标题
                AddInternal(new OsuSpriteText
                {
                    Text = $"Column {column.Key + 1}",
                    Font = OsuFont.GetFont(size: 14),
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopCentre
                });

                // 添加图表
                AddInternal(new HitEventTimingDistributionGraph(column.ToList())
                {
                    RelativeSizeAxes = Axes.None,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.BottomLeft,
                    Rotation = 90
                });
            }

            updateLayout();
        }

        protected override void Update()
        {
            base.Update();

            if (InternalChildren.Count == 0 || DrawWidth <= 0)
                return;

            updateLayout();
        }

        private void updateLayout()
        {
            int columnCount = hitEventsByColumn.Count;
            if (columnCount == 0) return;

            float effectiveWidth = DrawWidth - (2 * horizontal_margin);
            float totalSpacingWidth = horizontal_spacing_ratio * (columnCount - 1);
            float columnWidthRatio = (1f - totalSpacingWidth) / columnCount;
            float xPosition = horizontal_margin;

            for (int i = 0; i < columnCount; i++)
            {
                float columnWidth = columnWidthRatio * effectiveWidth;
                float spacingWidth = horizontal_spacing_ratio * effectiveWidth;

                int titleIndex = i * 2;
                int graphIndex = i * 2 + 1;

                // 更新标题位置
                if (titleIndex < InternalChildren.Count && InternalChildren[titleIndex] is OsuSpriteText titleText)
                {
                    titleText.X = xPosition + columnWidth / 2;
                    titleText.Y = 0;
                }

                // 更新图表位置和尺寸
                if (graphIndex < InternalChildren.Count && InternalChildren[graphIndex] is HitEventTimingDistributionGraph graph)
                {
                    graph.Width = DrawHeight - top_margin - bottom_margin;
                    graph.Height = columnWidth;
                    graph.X = xPosition;
                    graph.Y = top_margin;
                }

                xPosition += columnWidth + spacingWidth;
            }
        }
    }
}
