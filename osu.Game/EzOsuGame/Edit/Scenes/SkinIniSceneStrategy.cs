// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SkinIni;

        public LocalisableString TabTitle => EzEditorStrings.TAB_SKIN_INI;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) => new EzSkinEditorPreviewHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            if (context.SkinIniSession is not { IsSupported: true })
                return Array.Empty<EzSkinEditorSidebarGroupDefinition>();

            return new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_GENERAL,
                    CreateContent = () => new EzSkinEditorSkinIniGeneralSection(context.SkinIniSession, context.ComparisonSnapshot),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_COLOURS,
                    CreateContent = () => new EzSkinEditorSkinIniColoursSection(context.SkinIniSession, context.ComparisonSnapshot),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_MODE,
                    CreateContent = () => new EzSkinEditorSkinIniManiaSection(context.SkinIniSession, context.ComparisonSnapshot),
                },
            };
        }

        public Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context)
        {
            if (context.SkinIniSession is not { IsSupported: true })
                return null;

            return new SkinIniSaveFooter(context.CommitSkinIni);
        }
    }

    internal partial class SkinIniSaveFooter : Container
    {
        private readonly Action? commitSkinIni;

        public SkinIniSaveFooter(Action? commitSkinIni)
        {
            this.commitSkinIni = commitSkinIni;
            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Child = new SaveButton
            {
                Text = EzEditorStrings.SKIN_INI_SAVE_BUTTON,
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Action = () => commitSkinIni?.Invoke(),
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
