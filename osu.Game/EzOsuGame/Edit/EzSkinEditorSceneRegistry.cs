// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Scenes;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSceneRegistry
    {
        /// <summary>
        /// Secondary scenes in display order. The order also drives the left scene rail within each group.
        /// </summary>
        public static IReadOnlyList<IEzSkinEditorSceneStrategy> All { get; } = new IEzSkinEditorSceneStrategy[]
        {
            new SongSelectSceneStrategy(),
            new BeatmapPreviewSceneStrategy(),
            new GameplaySceneStrategy(),
            new AppearanceSceneStrategy(),
            new NoteSceneStrategy(),
            new SkinIniSceneStrategy(),
            new SkinSpecificSceneStrategy(),
            new EditorColouringSceneStrategy(),
        };

        /// <summary>
        /// Top-level groups in scene-bar order.
        /// </summary>
        public static IReadOnlyList<EzEditorSceneGroup> Groups { get; } = new[]
        {
            EzEditorSceneGroup.SongSelect,
            EzEditorSceneGroup.Gameplay,
            EzEditorSceneGroup.BeatmapEditor
        };

        public static IEnumerable<IEzSkinEditorSceneStrategy> ForGroup(EzEditorSceneGroup group) => All.Where(s => s.Group == group);

        public static IEzSkinEditorSceneStrategy Get(EzEditorSceneType sceneType) =>
            All.First(s => s.SceneType == sceneType);

        public static EzEditorSceneGroup GroupOf(EzEditorSceneType sceneType) => Get(sceneType).Group;

        /// <summary>
        /// Whether a scene presents an inserted screen whose skinnable HUD containers can be edited.
        /// Single source for the editor HUD layer's capability check.
        /// </summary>
        public static bool SupportsHudLayer(EzEditorSceneType sceneType) => Get(sceneType).SupportsHudLayer;

        public static EzLayoutRoot LayoutRootOf(EzEditorSceneType sceneType) => Get(sceneType).LayoutRoot;

        public static EzEditorSceneType ResolveCurrentScene(IScreen? currentScreen) => ResolveCurrentScene(currentScreen?.GetType());

        public static EzEditorSceneType ResolveCurrentScene(Type? screenType)
        {
            if (screenType != null && typeof(SoloSongSelect).IsAssignableFrom(screenType))
                return EzEditorSceneType.SongSelectHud;

            if (screenType != null && typeof(Player).IsAssignableFrom(screenType))
                return EzEditorSceneType.GameplayHud;

            return EzEditorSceneType.GameplayHud;
        }
    }
}
