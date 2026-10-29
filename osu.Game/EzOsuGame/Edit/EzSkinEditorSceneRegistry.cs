// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Scenes;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSceneRegistry
    {
        public static IReadOnlyList<IEzSkinEditorSceneStrategy> All { get; } = new IEzSkinEditorSceneStrategy[]
        {
            new SongSelectSceneStrategy(),
            new GameplaySceneStrategy(),
            new AppearanceSceneStrategy(),
            new SkinIniSceneStrategy(),
            new SkinSpecificSceneStrategy(),
            new NoteSceneStrategy(),
        };

        public static IEzSkinEditorSceneStrategy Get(EzSkinEditorSceneType sceneType) =>
            All.First(s => s.SceneType == sceneType);

        /// <summary>
        /// Whether a scene presents an inserted screen whose skinnable HUD containers can be edited.
        /// Single source for the editor HUD layer's capability check.
        /// </summary>
        public static bool SupportsHudLayer(EzSkinEditorSceneType sceneType) => Get(sceneType).SupportsHudLayer;

        /// <summary>
        /// The Ez layout container target a scene edits, or null when the scene has no editable HUD.
        /// Keeps the scene-to-target mapping out of the HUD layer.
        /// </summary>
        public static GlobalSkinnableContainers? ResolveLayoutTarget(EzSkinEditorSceneType sceneType) => sceneType switch
        {
            EzSkinEditorSceneType.Gameplay => GlobalSkinnableContainers.MainHUDComponents,
            EzSkinEditorSceneType.SongSelect => GlobalSkinnableContainers.SongSelect,
            _ => null,
        };

        public static EzSkinEditorSceneType ResolveCurrentScene(IScreen? currentScreen) => ResolveCurrentScene(currentScreen?.GetType());

        public static EzSkinEditorSceneType ResolveCurrentScene(Type? screenType)
        {
            if (screenType != null && typeof(SoloSongSelect).IsAssignableFrom(screenType))
                return EzSkinEditorSceneType.SongSelect;

            if (screenType != null && typeof(Player).IsAssignableFrom(screenType))
                return EzSkinEditorSceneType.Gameplay;

            return EzSkinEditorSceneType.Gameplay;
        }
    }
}
