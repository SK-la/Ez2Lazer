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
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Single-row career chips: PP, duration, keys, KPS, and score count.
    /// </summary>
    public partial class EzLocalProfileCareerSummaryRow : FillFlowContainer
    {
        private readonly EzLocalProfileRulesetStats stats;

        public EzLocalProfileCareerSummaryRow(EzLocalProfileRulesetStats stats)
        {
            this.stats = stats;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColours)
        {
            if (stats.ScoreCount == 0 && stats.TotalKeys == 0 && stats.TotalPp <= 0 && stats.TotalDurationMs <= 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_PP, EzLocalProfileFormat.FormatPp(stats.TotalPp), EzLocalProfileColours.Pp(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_DURATION, EzLocalProfileFormat.FormatDuration(stats.TotalDurationMs), EzLocalProfileColours.Duration(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_KEYS, stats.TotalKeys.ToString("N0"), EzLocalProfileColours.KeyCount(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_AVG_KPS, formatKps(stats.AvgKps), EzLocalProfileColours.AvgRate(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_MAX_KPS, formatKps(stats.MaxKps), EzLocalProfileColours.Peak(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_SCORE_COUNT, stats.ScoreCount.ToString("N0"), EzLocalProfileColours.Plays(colours)));
        }

        private static string formatKps(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public partial class EzLocalProfileMetricRow : FillFlowContainer
    {
        private readonly EzLocalProfileRulesetStats stats;

        public EzLocalProfileMetricRow(EzLocalProfileRulesetStats stats)
        {
            this.stats = stats;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColours)
        {
            if (stats.ScoreCount == 0 && stats.TotalKeys == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_KEYS, stats.TotalKeys.ToString("N0"), EzLocalProfileColours.KeyCount(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_AVG_KPS, formatKps(stats.AvgKps), EzLocalProfileColours.AvgRate(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_MAX_KPS, formatKps(stats.MaxKps), EzLocalProfileColours.Peak(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_SCORE_COUNT, stats.ScoreCount.ToString("N0"), EzLocalProfileColours.Plays(colours)));
        }

        private static string formatKps(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// PP + play-duration chips shown for every ruleset.
    /// </summary>
    public partial class EzLocalProfilePerformanceRow : FillFlowContainer
    {
        private readonly EzLocalProfileRulesetStats stats;

        public EzLocalProfilePerformanceRow(EzLocalProfileRulesetStats stats)
        {
            this.stats = stats;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(10);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour osuColours)
        {
            if (stats.ScoreCount == 0 && stats.TotalPp <= 0 && stats.TotalDurationMs <= 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_PP, EzLocalProfileFormat.FormatPp(stats.TotalPp), EzLocalProfileColours.Pp(osuColours)));
            Add(EzLocalProfileMetricChip.Create(EzSettingsProfile.LOCAL_PROFILE_TOTAL_DURATION, EzLocalProfileFormat.FormatDuration(stats.TotalDurationMs), EzLocalProfileColours.Duration(osuColours)));
        }
    }

    internal partial class EzLocalProfileMetricChip : Container
    {
        private readonly Color4? valueColour;
        private readonly ProfileValueDisplay display;

        public static Drawable Create(LocalisableString title, string value, Color4? valueColour = null)
        {
            var display = new ProfileValueDisplay { Title = title };
            display.Content.Text = value;
            return new EzLocalProfileMetricChip(display, valueColour);
        }

        private EzLocalProfileMetricChip(ProfileValueDisplay display, Color4? valueColour)
        {
            this.display = display;
            this.valueColour = valueColour;

            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 8;
            Child = new Container
            {
                AutoSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 14, Vertical = 10 },
                Child = display,
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

            if (valueColour is Color4 colour)
                display.Content.Colour = colour;
        }
    }
}
