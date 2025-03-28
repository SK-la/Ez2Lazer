// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BarHitErrorMeterStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.BarHitErrorMeter";

        /// <summary>
        /// "JudgementIcon Fade Out Duration"
        /// </summary>
        public static LocalisableString JudgementIconFadeOutDuration => new TranslatableString(getKey(@"judgement_icon_fade_out_duration"), @"JudgementIcon Fade Out Duration");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}