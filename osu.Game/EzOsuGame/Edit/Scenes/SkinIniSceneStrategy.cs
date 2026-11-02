// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class SkinIniSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;
        private SkinIniSaveFooter? footer;

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SkinIni;

        public EzSkinEditorSceneGroup Group => EzSkinEditorSceneGroup.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.TAB_SKIN_INI;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new EzSkinEditorSkinIniGeneralSection(),
            new EzSkinEditorSkinIniColoursSection(),
            new EzSkinEditorSkinIniManiaSection(),
        };

        public Drawable? Footer => footer ??= new SkinIniSaveFooter();
    }

    internal partial class SkinIniSaveFooter : Container
    {
        public SkinIniSaveFooter()
        {
            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        [BackgroundDependencyLoader]
        private void load(EzSkinEditorSidebarActions actions)
        {
            Child = new SaveButton
            {
                Text = EzEditorStrings.SKIN_INI_SAVE_BUTTON,
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Action = () => actions.CommitSkinIni?.Invoke(),
            };
        }

        private partial class SaveButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundColour = colours.Green3;
                Content.CornerRadius = 5;
            }
        }
    }
}
