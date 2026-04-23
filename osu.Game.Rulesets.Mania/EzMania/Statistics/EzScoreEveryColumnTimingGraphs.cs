// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania专用的偏移分布图，显示每列判定偏移时间的分布情况.
    /// <para></para>主要使用 <see cref="HitEvent"/> 中的 <see cref="HitEvent.TimeOffset"/> 来计算每列的平均偏移、稳定率等统计数据，并以图表和表格的形式展示出来。
    /// </summary>
    public partial class EzScoreEveryColumnTimingGraphs : CompositeDrawable
    {
        private const float horizontal_spacing_ratio = 0.015f;
        private const float top_margin = 20;
        private const float bottom_margin = 10;
        private const float horizontal_margin = 10;

        private readonly List<IGrouping<int, HitEvent>> hitEventsByColumn;
        private float lastDrawWidth = -1;
        private float lastDrawHeight = -1;

        public EzScoreEveryColumnTimingGraphs(ScoreInfo score)
        {
            hitEventsByColumn = score.HitEvents
                                     .Where(e => e.HitObject is ManiaHitObject)
                                     .GroupBy(e => ((ManiaHitObject)e.HitObject).Column)
                                     .OrderBy(g => g.Key)
                                     .ToList();

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

            if (DrawWidth != lastDrawWidth || DrawHeight != lastDrawHeight)
            {
                updateLayout();
                lastDrawWidth = DrawWidth;
                lastDrawHeight = DrawHeight;
            }
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
