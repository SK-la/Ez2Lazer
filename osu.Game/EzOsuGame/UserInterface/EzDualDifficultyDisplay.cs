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
    /// Horizontal official <see cref="StarRatingDisplay"/> + conditional <see cref="EzDisplaySR"/> + the official
    /// <see cref="EzDisplayPP"/>.
    /// </summary>
    public partial class EzDualDifficultyDisplay : FillFlowContainer
    {
        public StarRatingDisplay Official { get; }

        public EzDisplaySR SunnySR { get; }

        public EzDisplayPP Performance { get; }

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

            SunnySR = new EzDisplaySR(null, size, animated)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Scale = new Vector2(scale),
            };

            Performance = new EzDisplayPP(size)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Scale = new Vector2(scale),
            };

            Children = new Drawable[]
            {
                Official,
                SunnySR,
                Performance,
            };

            SunnySR.Hide();
            Performance.Hide();
        }

        public void UpdateOfficial(StarDifficulty starDifficulty) => Official.Current.Value = starDifficulty;

        /// <summary>
        /// Stored skill star for the selected readout, before a live pass is applied.
        /// </summary>
        public void UpdateFromBeatmap(BeatmapInfo beatmap)
            => ApplySkillStar(beatmap, beatmap.GetEzStarRating());

        /// <summary>
        /// Skill star beside the official one. Null still shows the pill when this ruleset has a skill readout.
        /// </summary>
        public void ApplySkillStar(BeatmapInfo beatmap, double? skillStar)
        {
            if (!ShouldShowSunnySRDisplay(beatmap))
            {
                ResetSunnySR();
                return;
            }

            SunnySR.Show();

            if (SunnySR.Current.Value != skillStar)
                SunnySR.Current.Value = skillStar;
        }

        public void ResetSunnySR()
        {
            SunnySR.Current.Value = null;
            SunnySR.Hide();
        }

        /// <summary>
        /// Shows the official performance points beside the stars. Null or a negative value hides the pill.
        /// </summary>
        public void ApplyPerformance(double? points)
        {
            if (points == null || !double.IsFinite(points.Value) || points.Value < 0)
            {
                Performance.Current.Value = null;
                Performance.Hide();
                return;
            }

            Performance.Current.Value = points.Value;
            Performance.Show();
        }

        /// <summary>
        /// Whether this chart has an Ez star to show beside the official one. Any ruleset whose selected system
        /// carries a skill vector does; the analysis provider only decides the Sunny rating, not the pill's presence.
        /// </summary>
        public static bool ShouldShowSunnySRDisplay(BeatmapInfo beatmap)
            => EzRulesetReadout.Selected(beatmap.Ruleset.OnlineID)?.SkillRating != EzAnalysisSystem.None;
    }
}
