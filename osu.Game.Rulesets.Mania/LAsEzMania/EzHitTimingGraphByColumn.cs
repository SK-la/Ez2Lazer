// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Statistics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Ranking.Statistics;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    /// <summary>
    /// A graph which displays the distribution of hit timing for each column in a series of <see cref="HitEvent"/>s.
    /// </summary>
    public partial class EzHitTimingGraphByColumn : CompositeDrawable
    {
        /// <summary>
        /// The currently displayed hit events.
        /// </summary>
        private readonly IReadOnlyList<HitEvent> hitEvents;

        /// <summary>
        /// The number of columns in the beatmap.
        /// </summary>
        private readonly int columnCount;

        /// <summary>
        /// Creates a new <see cref="EzHitTimingGraphByColumn"/>.
        /// </summary>
        /// <param name="hitEvents">The <see cref="HitEvent"/>s to display the timing distribution of.</param>
        /// <param name="columnCount">The number of columns in the beatmap.</param>
        public EzHitTimingGraphByColumn(IReadOnlyList<HitEvent> hitEvents, int columnCount)
        {
            this.hitEvents = hitEvents;
            this.columnCount = columnCount;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEvents.Count == 0)
                return;

            var columnGraphs = new List<Drawable>();

            for (int i = 0; i < columnCount; i++)
            {
                var columnEvents = hitEvents.Where(h => ((ManiaHitObject)h.HitObject).Column == i).ToList();

                if (columnEvents.Count == 0)
                    continue;

                int perfect = columnEvents.Count(h => h.Result == HitResult.Perfect);
                int great = columnEvents.Count(h => h.Result == HitResult.Great);
                int good = columnEvents.Count(h => h.Result == HitResult.Good);
                int ok = columnEvents.Count(h => h.Result == HitResult.Ok);
                int meh = columnEvents.Count(h => h.Result == HitResult.Meh);
                int miss = columnEvents.Count(h => h.Result == HitResult.Miss);
                int total = perfect + great + good + ok + meh + miss;

                int perfectLate = columnEvents.Count(h => h.Result == HitResult.Perfect && h.TimeOffset > 0);
                int greatLate = columnEvents.Count(h => h.Result == HitResult.Great && h.TimeOffset > 0);
                int goodLate = columnEvents.Count(h => h.Result == HitResult.Good && h.TimeOffset > 0);
                int okLate = columnEvents.Count(h => h.Result == HitResult.Ok && h.TimeOffset > 0);
                int mehLate = columnEvents.Count(h => h.Result == HitResult.Meh && h.TimeOffset > 0);
                int missLate = columnEvents.Count(h => h.Result == HitResult.Miss && h.TimeOffset > 0);
                int totalLate = perfectLate + greatLate + goodLate + okLate + mehLate + missLate;

                int perfectEarly = columnEvents.Count(h => h.Result == HitResult.Perfect && h.TimeOffset <= 0);
                int greatEarly = columnEvents.Count(h => h.Result == HitResult.Great && h.TimeOffset <= 0);
                int goodEarly = columnEvents.Count(h => h.Result == HitResult.Good && h.TimeOffset <= 0);
                int okEarly = columnEvents.Count(h => h.Result == HitResult.Ok && h.TimeOffset <= 0);
                int mehEarly = columnEvents.Count(h => h.Result == HitResult.Meh && h.TimeOffset <= 0);
                int missEarly = columnEvents.Count(h => h.Result == HitResult.Miss && h.TimeOffset <= 0);
                int totalEarly = perfectEarly + greatEarly + goodEarly + okEarly + mehEarly + missEarly;

                var totalColor = Color4Extensions.FromHex(@"ff8c00");
                var perfectColor = Color4Extensions.FromHex(@"99eeff");
                var greatColor = Color4Extensions.FromHex(@"66ccff");
                var goodColor = Color4Extensions.FromHex(@"b3d944");
                var okColor = Color4Extensions.FromHex(@"88b300");
                var mehColor = Color4Extensions.FromHex(@"ffcc22");
                var missColor = Color4Extensions.FromHex(@"ed1121");

                var columnContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical
                };

                columnContainer.Add(new HitEventTimingDistributionGraph(columnEvents)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 100,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new osuTK.Vector2(0.96f),
                    Margin = new MarginPadding { Top = 5, Bottom = 10 }
                });

                columnContainer.Add(new OsuSpriteText
                {
                    Text = $"Column {i + 1}",
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.Bold),
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre
                });

                columnContainer.Add(new AverageHitError(columnEvents)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new osuTK.Vector2(0.96f)
                });

                columnContainer.Add(new UnstableRate(columnEvents)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new osuTK.Vector2(0.96f)
                });

                columnContainer.Add(new SimpleStatisticTable(3, new[]
                {
                    new EzJudgementsItem(total.ToString(), "Total", totalColor),
                    new EzJudgementsItem(totalLate.ToString(), "Total (Late)", ColourInfo.GradientVertical(Colour4.White, totalColor)),
                    new EzJudgementsItem(totalEarly.ToString(), "Total (Early)", ColourInfo.GradientVertical(totalColor, Colour4.White)),
                    new EzJudgementsItem(perfect.ToString(), "Perfect", perfectColor),
                    new EzJudgementsItem(perfectLate.ToString(), "Perfect (Late)", ColourInfo.GradientVertical(Colour4.White, perfectColor)),
                    new EzJudgementsItem(perfectEarly.ToString(), "Perfect (Early)", ColourInfo.GradientVertical(perfectColor, Colour4.White)),
                    new EzJudgementsItem(great.ToString(), "Great", greatColor),
                    new EzJudgementsItem(greatLate.ToString(), "Great (Late)", ColourInfo.GradientVertical(Colour4.White, greatColor)),
                    new EzJudgementsItem(greatEarly.ToString(), "Great (Early)", ColourInfo.GradientVertical(greatColor, Colour4.White)),
                    new EzJudgementsItem(good.ToString(), "Good", goodColor),
                    new EzJudgementsItem(goodLate.ToString(), "Good (Late)", ColourInfo.GradientVertical(Colour4.White, goodColor)),
                    new EzJudgementsItem(goodEarly.ToString(), "Good (Early)", ColourInfo.GradientVertical(goodColor, Colour4.White)),
                    new EzJudgementsItem(ok.ToString(), "Ok", okColor),
                    new EzJudgementsItem(okLate.ToString(), "Ok (Late)", ColourInfo.GradientVertical(Colour4.White, okColor)),
                    new EzJudgementsItem(okEarly.ToString(), "Ok (Early)", ColourInfo.GradientVertical(okColor, Colour4.White)),
                    new EzJudgementsItem(meh.ToString(), "Meh", mehColor),
                    new EzJudgementsItem(mehLate.ToString(), "Meh (Late)", ColourInfo.GradientVertical(Colour4.White, mehColor)),
                    new EzJudgementsItem(mehEarly.ToString(), "Meh (Early)", ColourInfo.GradientVertical(mehColor, Colour4.White)),
                    new EzJudgementsItem(miss.ToString(), "Miss", missColor),
                    new EzJudgementsItem(missLate.ToString(), "Miss (Late)", ColourInfo.GradientVertical(Colour4.White, missColor)),
                    new EzJudgementsItem(missEarly.ToString(), "Miss (Early)", ColourInfo.GradientVertical(missColor, Colour4.White))
                })
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new osuTK.Vector2(0.96f)
                });

                columnGraphs.Add(columnContainer);
            }

            const int columns_per_row = 2;
            int rowCount = (columnGraphs.Count + columns_per_row - 1) / columns_per_row; // 向上取整得到行数

            var gridContent = new Drawable[rowCount][];

            for (int i = 0; i < rowCount; i++)
            {
                gridContent[i] = new Drawable[columns_per_row];

                for (int j = 0; j < columns_per_row; j++)
                {
                    int index = i * columns_per_row + j;

                    if (index < columnGraphs.Count)
                    {
                        gridContent[i][j] = columnGraphs[index];

                        if (columnCount % 2 == 1 && i == rowCount - 1 && j == 0)
                        {
                            var position = gridContent[i][j].Position;
                            position.X += 228;
                            gridContent[i][j].Position = position;
                        }
                    }
                    else
                        gridContent[i][j] = Empty();
                }
            }

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = Enumerable.Range(0, rowCount).Select(_ => new Dimension(GridSizeMode.AutoSize)).ToArray(),
                ColumnDimensions = Enumerable.Range(0, columns_per_row).Select(_ => new Dimension()).ToArray(),
                Content = gridContent
            };
        }
    }
}
