// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class RulesetSection : SettingsSection
    {
        public RulesetSection(IEnumerable<SettingsSubsection> subsections)
        {
            foreach (var subsection in subsections)
                Add(subsection);
        }

        public override LocalisableString Header => RulesetSettingsStrings.Rulesets;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Rulesets
        };
    }
}
