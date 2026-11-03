// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameSection : SettingsSection
    {
        public override LocalisableString Header => EzSettingsStrings.EZ_GAME_SECTION_HEADER;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.GameplayB
        };

        protected override bool ShowSubsectionIndex => true;

        public EzGameSection()
        {
            Children = new Drawable[]
            {
                new EzUISettings(),
                new EzPetSettings(),
                new EzGameplaySettings(),
                new EzQuickRotationSettings(),
                new EzStdSettings(),
                new EzGameModeSettings(),
                new EzSkillSettings(),
                new ServerSettings(),
                new EzExperimentalSettings(),
            };
        }
    }
}
