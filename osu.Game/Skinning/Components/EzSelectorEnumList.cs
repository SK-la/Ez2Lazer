// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Overlays.Settings;

namespace osu.Game.Skinning.Components
{
    public partial class EzSelectorEnumList : SettingsDropdown<EzSelectorNameSet>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            Items = Enum.GetValues(typeof(EzSelectorNameSet)).Cast<EzSelectorNameSet>().ToList();
        }
    }

    public partial class AnchorDropdown : SettingsDropdown<Anchor>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            // 限制选项范围
            Items = new List<Anchor>
            {
                Anchor.TopCentre,
                Anchor.Centre,
                Anchor.BottomCentre
            };
        }
    }

    public enum EzComEffectType
    {
        Scale,
        Bounce,
        None
    }

    public enum EzSelectorNameSet
    {
        // ReSharper disable InconsistentNaming
        EZ2DJ_1st,
        EZ2DJ_1stSE,
        EZ2DJ_2nd,
        EZ2DJ_3rd,
        EZ2DJ_4th,
        EZ2DJ_6th,
        EZ2DJ_7th,
        AIR,
        AZURE_EXPRESSION,
        CV_CRAFT,
        D2D_Station,
        Dark_Concert,
        DJMAX,
        EC_1304,
        EC_Wheel,
        EVOLVE,
        EZ2ON,
        FIND_A_WAY,
        Fortress2,
        Fortress3_Future,
        Fortress3_Gear,
        Fortress3_Green,
        Fortress3_Modern,
        GC,
        GC_EZ,
        Gem,
        HX_1121,
        HX_STANDARD,
        JIYU,
        Kings,
        Limited,
        NIGHT_FALL,
        O2_A9100,
        O2_EA05,
        O2_Jam,
        Platinum,
        QTZ_01,
        QTZ_02,
        REBOOT,
        SG_701,
        SH_512,
        Star,
        TANOc,
        TANOc2,
        TECHNIKA,
        TIME_TRAVELER,
        TOMATO,
        Turtle,
        Various_Ways,
        ArcadeScore,
        // ReSharper restore InconsistentNaming
    }
}
