// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Determines whether a local score may be submitted to online servers based on gameplay session snapshots on <see cref="ScoreInfo"/>,
    /// not live <see cref="Ez2ConfigManager"/> values (which can change on the results screen).
    /// </summary>
    public static class EzOnlineScoreSubmissionPolicy
    {
        public const double DEFAULT_ACCURACY_CUTOFF_A = 0.9;
        public const double DEFAULT_ACCURACY_CUTOFF_S = 0.95;

        /// <summary>
        /// Records offset and accuracy cutoff settings at the start of a play session.
        /// Mania hit/health modes are stored separately via <see cref="EzManiaScoreModeExtensions.ApplyManiaGameplayModes"/>.
        /// </summary>
        public static void ApplySessionSettingsSnapshot(ScoreInfo score, Ez2ConfigManager config)
        {
            score.SessionAccuracyCutoffA = config.Get<double>(Ez2Setting.AccuracyCutoffA);
            score.SessionAccuracyCutoffS = config.Get<double>(Ez2Setting.AccuracyCutoffS);
            score.SessionOffsetPlusMania = config.Get<double>(Ez2Setting.OffsetPlusMania);
            score.SessionOffsetPlusNonMania = config.Get<double>(Ez2Setting.OffsetPlusNonMania);
            score.SessionSettingsCaptured = true;
        }

        public static bool AllowsOfficialSubmission(ScoreInfo score) => AllowsOfficialSubmission(score, out _);

        public static bool AllowsOfficialSubmission(ScoreInfo score, out string blockReason)
        {
            blockReason = string.Empty;

            if (score.Ruleset.OnlineID == 3)
            {
                if (!score.TryGetManiaGameplayModes(out int hitMode, out int healthMode))
                {
                    blockReason = "mania gameplay modes are not set on this score";
                    return false;
                }

                if (hitMode != (int)EzEnumHitMode.Lazer || healthMode != (int)EzEnumHealthMode.Lazer)
                {
                    blockReason = $"ManiaHitMode={hitMode}, ManiaHealthMode={healthMode} (Lazer required)";
                    return false;
                }

                if (score.SessionOffsetPlusMania != 0)
                {
                    blockReason = $"SessionOffsetPlusMania={score.SessionOffsetPlusMania:0.####}";
                    return false;
                }

                if (!hasDefaultAccuracyCutoffs(score.SessionAccuracyCutoffA, score.SessionAccuracyCutoffS))
                {
                    blockReason = $"SessionAccuracyCutoffA={score.SessionAccuracyCutoffA:0.####}, SessionAccuracyCutoffS={score.SessionAccuracyCutoffS:0.####}";
                    return false;
                }

                return true;
            }

            if (score.SessionOffsetPlusNonMania != 0)
            {
                blockReason = $"SessionOffsetPlusNonMania={score.SessionOffsetPlusNonMania:0.####}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Matches previous bindable logic: submission is allowed if at least one grade cutoff is still at its default.
        /// </summary>
        private static bool hasDefaultAccuracyCutoffs(double cutoffA, double cutoffS) =>
            isDefaultCutoff(cutoffA, DEFAULT_ACCURACY_CUTOFF_A) || isDefaultCutoff(cutoffS, DEFAULT_ACCURACY_CUTOFF_S);

        private static bool isDefaultCutoff(double value, double defaultValue) => Math.Abs(value - defaultValue) < 0.0001;
    }
}
