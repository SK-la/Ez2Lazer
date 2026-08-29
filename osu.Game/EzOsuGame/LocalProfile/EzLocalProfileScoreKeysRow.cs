// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileScoreKeysRow : FillFlowContainer
    {
        public EzLocalProfileScoreKeysRow()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8);
        }

        public void UpdateRow(EzLocalProfileDrillScoreRow? row, EzLocalProfileScoreDisplayData? data = null)
        {
            Clear();

            if (row == null)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_SELECT_SCORE,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            if (data != null)
            {
                Add(createChip("PP", data.Value.Pp is double pp ? $"{EzLocalProfileFormat.FormatPp(pp)}pp" : "—"));
                Add(createChip(BeatmapsetsStrings.ShowScoreboardHeadersAccuracy, data.Value.AccuracyText));
                Add(createChip(
                    BeatmapsetsStrings.ShowScoreboardHeadersCombo,
                    $"{data.Value.MaxCombo}x"));
            }

            string avgKps = row.KpsAvg > 0 ? formatKps(row.KpsAvg) : "—";
            string maxKps = row.KpsMax > 0 ? formatKps(row.KpsMax) : "—";

            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_TOTAL_KEYS, row.TotalKeys.ToString("N0", CultureInfo.InvariantCulture)));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_AVG_KPS, avgKps));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_MAX_KPS, maxKps));
            Add(createChip(EzSettingsStrings.LOCAL_PROFILE_SCORE_COUNT, "1"));
        }

        private static Drawable createChip(LocalisableString title, string value)
        {
            var chip = EzLocalProfileMetricChip.Create(title, value);
            chip.Anchor = Anchor.CentreLeft;
            chip.Origin = Anchor.CentreLeft;
            return chip;
        }

        private static string formatKps(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
