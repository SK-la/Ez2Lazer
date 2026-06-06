// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class DynamicSpeedAdjustStrings
    {
        public static readonly LocalisableString RATE_CHANGE_STEP_LABEL = new EzLocalizationManager.EzLocalisableString("动态变速幅度", "Dynamic Rate Change Step");

        public static readonly LocalisableString RATE_CHANGE_STEP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "每次动态变速（含 Miss 降速）允许的最大倍率变化量。例如 0.002 表示 1.00x 下次最多变为 1.002x 或 0.998x。",
            "Maximum rate delta per dynamic adjustment (including miss slowdown). For example, 0.002 means 1.00x can change to at most 1.002x or 0.998x.");
    }
}
