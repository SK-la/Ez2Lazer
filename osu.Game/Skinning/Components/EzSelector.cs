// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTabletDriver.Plugin;
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

    public partial class OffsetNumberNameSelector : SettingsDropdown<string>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items = new List<string>
            {
                "Air",
                "Aqua",
                "Cricket",
                "D2D",
                "DarkConcert",
                "DJMAX",
                "EMOTIONAL",
                "ENDLESS",
                "EZ2AC-AE",
                "EZ2AC-CV",
                "EZ2AC-EVOLVE",
                "EZ2AC-TT",
                "EZ2DJ-1thSE",
                "EZ2DJ-2nd",
                "EZ2DJ-4th",
                "EZ2DJ-6th",
                "EZ2DJ-7th",
                "EZ2DJ-Platinum",
                "EZ2ON",
                "F3-ANCIENT",
                "F3-CONTEMP",
                "F3-FUTURE",
                "F3-MODERN",
                "FiND A WAY",
                "FORTRESS2",
                "GC-TYPE2",
                "Gem",
                "Gem2",
                "Gold",
                "HX STD",
                "HX STD黄色",
                "HX-1121",
                "Kings",
                "M250",
                "NIGHTFALL",
                "NIGHTwhite",
                "QTZ-02",
                "REBOOT",
                "REBOOT GOLD",
                "SG-701",
                "SH-512",
                "Star",
                "TCEZ-001",
                "TECHNIKA",
                "Tomato",
                "VariousWays",
            };
            Log.Debug("Items", Items.ToString());
        }
    }
}
