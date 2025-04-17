// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
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
}
