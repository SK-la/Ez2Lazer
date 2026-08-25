// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Header.Components;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileMetricRow : FillFlowContainer
    {
        public EzLocalProfileMetricRow(EzLocalProfileRulesetStats stats)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);

            if (stats.ScoreCount == 0 && stats.TotalKeys == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_TOTAL_KEYS, stats.TotalKeys.ToString("N0")));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_AVG_KPS, formatKps(stats.AvgKps)));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_MAX_KPS, formatKps(stats.MaxKps)));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_SCORE_COUNT, stats.ScoreCount.ToString("N0")));
        }

        private static Drawable createChip(LocalisableString title, string value)
        {
            var display = new ProfileValueDisplay { Title = title };
            display.Content.Text = value;

            return new MetricChip(display);
        }

        private static string formatKps(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private partial class MetricChip : Container
        {
            public MetricChip(Drawable content)
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
                Child = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 14, Vertical = 10 },
                    Child = content,
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                AddInternal(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background5,
                    Depth = float.MaxValue,
                });
            }
        }
    }
}
