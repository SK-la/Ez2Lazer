// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// The tuning coefficients the stored Sunny ratings were produced with, and whether the current settings differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two coefficients are read from the settings on every computation, so an edit silently changes what the next
    /// rating is scored with. That is deliberate - they are a permanent revision of the user's algorithm, not a
    /// switch - but it means a library rated with the previous pair is stale without any of its rows looking stale.
    /// </para>
    /// <para>
    /// Recording what the last completed pass used is what makes that visible: while the settings differ from the
    /// record, the Sunny system is treated as out of date, and the library is recomputed on the next launch. A value
    /// edited and then edited back matches the record again, so an abandoned experiment does not queue a rebuild.
    /// </para>
    /// </remarks>
    public static class EzSSRTuningSnapshot
    {
        /// <summary>Whether the settings currently differ from the tuning the stored ratings were produced with.</summary>
        public static bool IsPending => current() != Applied;

        /// <summary>The tuning the last completed Sunny pass was scored with.</summary>
        public static EzSSRTuning Applied
        {
            get
            {
                Ez2ConfigManager config = GlobalConfigStore.EzConfig;

                return new EzSSRTuning(
                    stored(config, Ez2Setting.SsrAppliedRescaleThreshold, EzSSRTuning.FALLBACK.RescaleThreshold),
                    stored(config, Ez2Setting.SsrAppliedLnMultiplier, EzSSRTuning.FALLBACK.LnMultiplier));
            }
        }

        /// <summary>Records the tuning in force as the one the stored ratings were produced with.</summary>
        public static void Record()
        {
            Ez2ConfigManager config = GlobalConfigStore.EzConfig;
            EzSSRTuning tuning = current(config);

            config.SetValue(Ez2Setting.SsrAppliedRescaleThreshold, tuning.RescaleThreshold);
            config.SetValue(Ez2Setting.SsrAppliedLnMultiplier, tuning.LnMultiplier);
        }

        private static EzSSRTuning current() => current(GlobalConfigStore.EzConfig);

        private static EzSSRTuning current(Ez2ConfigManager config)
            => new EzSSRTuning(
                stored(config, Ez2Setting.SsrRescaleThreshold, EzSSRTuning.FALLBACK.RescaleThreshold),
                stored(config, Ez2Setting.SsrLnMultiplier, EzSSRTuning.FALLBACK.LnMultiplier));

        /// <summary>
        /// One coefficient, with the same guard <see cref="EzSSREngine.Tuning"/> applies: a stored value that would
        /// make every rating degenerate reads as the fallback, so an edited-then-restored file still matches.
        /// </summary>
        private static double stored(Ez2ConfigManager config, Ez2Setting setting, double fallback)
        {
            double value = config.Get<double>(setting);
            return double.IsFinite(value) && value > 0 ? value : fallback;
        }
    }
}
