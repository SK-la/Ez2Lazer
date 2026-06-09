// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class SkinIniSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SkinIni;

        public LocalisableString TabTitle => "skin.ini";

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorPreviewHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = "Skin.ini",
                    CreateContent = () => new EzSkinEditorSkinIniPlaceholderSection(),
                },
            };

        public Drawable CreateSidebarFooter(EzSkinEditorSceneContext context) =>
            new SkinIniSaveFooter(context);
    }

    internal partial class SkinIniSaveFooter : Container
    {
        private readonly EzSkinEditorSceneContext context;

        public SkinIniSaveFooter(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Child = new SaveButton
            {
                Text = "保存 Skin.ini",
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Action = () => context.RequestSceneRefresh?.Invoke(),
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
