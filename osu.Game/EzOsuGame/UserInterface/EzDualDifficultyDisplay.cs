// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Horizontal official <see cref="StarRatingDisplay"/> + conditional <see cref="EzDisplaySR"/>.
    /// </summary>
    public partial class EzDualDifficultyDisplay : FillFlowContainer
    {
        public StarRatingDisplay Official { get; }

        public EzDisplaySR Xxy { get; }

        public EzDualDifficultyDisplay(StarRatingDisplaySize size = StarRatingDisplaySize.Regular, bool animated = false, float scale = 1f)
        {
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(3);
            AutoSizeAxes = Axes.Both;

            Official = new StarRatingDisplay(default, size, animated)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Scale = new Vector2(scale),
            };

            Xxy = new EzDisplaySR(EzManiaSummary.EMPTY, size, animated)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Scale = new Vector2(scale),
            };

            Children = new Drawable[]
            {
                Official,
                Xxy,
            };

            Xxy.Hide();
        }

        public void UpdateOfficial(StarDifficulty starDifficulty) => Official.Current.Value = starDifficulty;

        public void UpdateFromBeatmap(BeatmapInfo beatmap)
        {
            if (!ShouldShowXxyDisplay(beatmap))
            {
                ResetXxy();
                return;
            }

            Xxy.Show();
            Xxy.Current.Value = beatmap.ToEzManiaSummaryForDisplay();
        }

        public void ApplyAnalysisMetrics(BeatmapInfo beatmap, in EzSongSelectAnalysisDisplay.PanelMetrics metrics)
        {
            if (!ShouldShowXxyDisplay(beatmap))
            {
                ResetXxy();
                return;
            }

            Xxy.Show();

            var summaryForDisplay = metrics.ManiaSummary ?? beatmap.ToEzManiaSummaryForDisplay();

            if (Xxy.Current.Value.XxySr != summaryForDisplay.XxySr)
                Xxy.Current.Value = summaryForDisplay;
        }

        public void ResetXxy()
        {
            Xxy.Current.Value = EzManiaSummary.EMPTY;
            Xxy.Hide();
        }

        public static bool ShouldShowXxyDisplay(BeatmapInfo beatmap)
            => EzAnalysisProviderBridge.HasAnalysisProvider(beatmap.Ruleset) && beatmap.SupportsXxyStarRating();
    }
}
