// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;

namespace osu.Game.Skinning.Components
{
    public partial class EzSelector
    {
        protected virtual string SetPath => @"Gameplay/Fonts/";

        public Bindable<string> Selected { get; } = new Bindable<string>();

        private Dropdown<string> dropdown = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            string[] folders = Directory.GetDirectories(SetPath)
                                        .Select(Path.GetFileName)
                                        .Where(name => name != null)
                                        .ToArray()!;

            InternalChild = dropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = folders
            };

            dropdown.Current.BindTo(Selected);

            if (folders.Length > 0)
                Selected.Value = folders[0];
        }

        public Dropdown<string> InternalChild { get; set; } = null!;
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

    public enum OffsetNumberName
    {
        // ReSharper disable InconsistentNaming
        Air,
        Aqua,
        Cricket,
        D2D,
        DarkConcert,
        DJMAX,
        EMOTIONAL,
        ENDLESS,
        EZ2AC_AE,
        EZ2AC_CV,
        EZ2AC_EVOLVE,
        EZ2AC_TT,
        EZ2DJ_1thSE,
        EZ2DJ_2nd,
        EZ2DJ_4th,
        EZ2DJ_6th,
        EZ2DJ_7th,
        EZ2DJ_Platinum,
        EZ2ON,
        F3_ANCIENT,
        F3_CONTEMP,
        F3_FUTURE,
        F3_MODERN,
        FiND_A_WAY,
        FORTRESS2,
        GC_TYPE2,
        Gem,
        Gem2,
        Gold,
        HX_STD,
        HX_STD_Yellow,
        HX_1121,
        Kings,
        M250,
        NIGHTFALL,
        NIGHTWhite,
        QTZ_02,
        REBOOT,
        REBOOT_GOLD,
        SG_701,
        SH_512,
        Star,
        TCEZ_001,
        TECHNIKA,
        Tomato,
        VariousWays

        // ReSharper restore InconsistentNaming
    }

    public partial class OffsetNumberNameSelector : SettingsDropdown<OffsetNumberName>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items = Enum.GetValues(typeof(OffsetNumberName)).Cast<OffsetNumberName>().ToList();
        }
    }
}
