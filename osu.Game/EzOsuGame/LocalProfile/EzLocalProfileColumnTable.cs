// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Transposed column table: metrics as rows, columns as headers.
    /// </summary>
    public partial class EzLocalProfileColumnTable : FillFlowContainer
    {
        public EzLocalProfileColumnTable(IReadOnlyList<EzLocalProfileManiaColumnStats> columns)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 2);

            var ordered = columns.OrderBy(c => c.ColumnIndex).ToList();

            if (ordered.Count == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 13),
                });
                return;
            }

            var headers = new LocalisableString[ordered.Count + 1];
            headers[0] = EzSettingsStrings.LOCAL_PROFILE_COL_HEADER;

            for (int i = 0; i < ordered.Count; i++)
                headers[i + 1] = $"#{ordered[i].ColumnIndex + 1}";

            Add(new MetricRow(headers, header: true, alternate: false));

            Add(new MetricRow(buildMetricRow(EzSettingsStrings.LOCAL_PROFILE_TOTAL_KEYS, ordered.Select(c => (LocalisableString)c.TotalKeys.ToString("N0"))), header: false, alternate: false));
            Add(new MetricRow(buildMetricRow(EzSettingsStrings.LOCAL_PROFILE_AVG_KPS, ordered.Select(c => (LocalisableString)formatKps(c.AvgKps))), header: false, alternate: true));
            Add(new MetricRow(buildMetricRow(EzSettingsStrings.LOCAL_PROFILE_MAX_KPS, ordered.Select(c => (LocalisableString)formatKps(c.MaxKps))), header: false, alternate: false));
            Add(new MetricRow(buildMetricRow(EzSettingsStrings.LOCAL_PROFILE_SCORE_COUNT, ordered.Select(c => (LocalisableString)c.ScoreCount.ToString("N0"))), header: false, alternate: true));
        }

        private static LocalisableString[] buildMetricRow(LocalisableString label, IEnumerable<LocalisableString> values)
        {
            var list = values.ToList();
            var row = new LocalisableString[list.Count + 1];
            row[0] = label;

            for (int i = 0; i < list.Count; i++)
                row[i + 1] = list[i];

            return row;
        }

        private static string formatKps(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private partial class MetricRow : Container
        {
            private readonly LocalisableString[] cells;
            private readonly bool header;
            private readonly bool alternate;

            public MetricRow(LocalisableString[] cells, bool header, bool alternate)
            {
                this.cells = cells;
                this.header = header;
                this.alternate = alternate;

                RelativeSizeAxes = Axes.X;
                Height = 30;
                Masking = true;
                CornerRadius = header ? 6 : 4;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                int count = cells.Length;
                float labelWidth = 0.18f;
                float valueWidth = count <= 1 ? 0.82f : 0.82f / (count - 1);

                var dimensions = new Dimension[count];
                dimensions[0] = new Dimension(GridSizeMode.Relative, labelWidth);

                for (int i = 1; i < count; i++)
                    dimensions[i] = new Dimension(GridSizeMode.Relative, valueWidth);

                var weight = header ? FontWeight.Bold : FontWeight.Regular;
                var colour = header ? colours.Content2 : colours.Content1;
                var labelColour = header ? colours.Content2 : colours.Content2;

                var content = new Drawable[count];
                content[0] = cell(cells[0], labelColour, FontWeight.Bold);

                for (int i = 1; i < count; i++)
                    content[i] = cell(cells[i], colour, weight);

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                        Alpha = header ? 1 : alternate ? 0.55f : 0,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 10 },
                        ColumnDimensions = dimensions,
                        Content = new[] { content }
                    }
                };
            }

            private static Drawable cell(LocalisableString text, Colour4 colour, FontWeight weight) => new TruncatingSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Colour = colour,
                Font = OsuFont.GetFont(size: 12, weight: weight),
                RelativeSizeAxes = Axes.X,
            };
        }
    }
}
