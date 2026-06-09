// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Edit.Scenes;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSceneRegistry
    {
        public static IReadOnlyList<IEzSkinEditorSceneStrategy> All { get; } = new IEzSkinEditorSceneStrategy[]
        {
            new AppearanceSceneStrategy(),
            new SizeSceneStrategy(),
            new ColourSceneStrategy(),
            new SkinIniSceneStrategy(),
        };

        public static IEzSkinEditorSceneStrategy Get(EzSkinEditorSceneType sceneType) =>
            All.First(s => s.SceneType == sceneType);
    }
}
