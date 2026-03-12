// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public static class EzProHelper
    {
        public static readonly HashSet<string> FREE_SIZE_STAGES = new HashSet<string>
        {
            "AZURE_EXPRESSION",
            "Celeste_Lumiere",
            "EC_Wheel",
            "EVOLVE",
            "Fortress3_Gear",
            "Fortress3_Modern",
            "GC",
            "NIGHT_FALL",
            "TANOc2",
            "TECHNIKA",
        };

        public enum EzEnumGameThemeNameForFreeSize
        {
            // ReSharper disable InconsistentNaming
            AZURE_EXPRESSION,
            Celeste_Lumiere,
            EC_Wheel,
            EVOLVE,
            Fortress3_Gear,
            Fortress3_Modern,
            GC,
            NIGHT_FALL,
            TANOc2,
            TECHNIKA,
        }
    }
}
