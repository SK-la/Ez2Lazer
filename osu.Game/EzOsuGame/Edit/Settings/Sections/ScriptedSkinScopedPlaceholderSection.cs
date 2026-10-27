// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// Placeholder for future scripted-skin [SkinConfig] reflection and csx persistence.
    /// </summary>
    public partial class ScriptedSkinScopedPlaceholderSection : FillFlowContainer
    {
        public ScriptedSkinScopedPlaceholderSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
            Padding = new MarginPadding(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = EzEditorStrings.SCRIPTED_SKIN_SCOPED_PLACEHOLDER,
                Font = OsuFont.Default.With(size: 14),
            };
        }
    }
}
