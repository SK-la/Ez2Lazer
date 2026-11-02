// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Scenes;
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
        };

        /// <summary>
        /// Top-level groups in scene-bar order.
        /// </summary>
        public static IReadOnlyList<EzSkinEditorSceneGroup> Groups { get; } = new[]
        {
            EzSkinEditorSceneGroup.SongSelect,
            EzSkinEditorSceneGroup.Gameplay,
        };

        public static IEnumerable<IEzSkinEditorSceneStrategy> ForGroup(EzSkinEditorSceneGroup group) => All.Where(s => s.Group == group);

        public static IEzSkinEditorSceneStrategy Get(EzSkinEditorSceneType sceneType) =>
            All.First(s => s.SceneType == sceneType);

        public static EzSkinEditorSceneGroup GroupOf(EzSkinEditorSceneType sceneType) => Get(sceneType).Group;

        /// <summary>
        /// Whether a scene presents an inserted screen whose skinnable HUD containers can be edited.
        /// Single source for the editor HUD layer's capability check.
        /// </summary>
        public static bool SupportsHudLayer(EzSkinEditorSceneType sceneType) => Get(sceneType).SupportsHudLayer;

        public static EzSkinEditorSceneType ResolveCurrentScene(IScreen? currentScreen) => ResolveCurrentScene(currentScreen?.GetType());

        public static EzSkinEditorSceneType ResolveCurrentScene(Type? screenType)
        {
            if (screenType != null && typeof(SoloSongSelect).IsAssignableFrom(screenType))
                return EzSkinEditorSceneType.SongSelectHud;

            if (screenType != null && typeof(Player).IsAssignableFrom(screenType))
                return EzSkinEditorSceneType.GameplayHud;

            return EzSkinEditorSceneType.GameplayHud;
        }
    }
}
