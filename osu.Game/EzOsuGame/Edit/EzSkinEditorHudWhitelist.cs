// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.HUD.MusicPlayer;
using osu.Game.EzOsuGame.HUD.Results;
using osu.Game.EzOsuGame.Screens.Menu;
using osu.Game.EzOsuGame.Screens.Visualizer;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Per-scene HUD library allow-list. Scene-specific types stay out of gameplay / song-select / skin HUD.
    /// </summary>
    public static class EzSkinEditorHudWhitelist
    {
        private static readonly HashSet<Type> results = new HashSet<Type>
        {
            typeof(EzResultsScorePanelList),
        };

        private static readonly HashSet<Type> music_player = new HashSet<Type>
        {
            typeof(EzVisualizerBeatmapLogo),
            typeof(EzVisualizerPlaybackBar),
            typeof(EzMenuLogoVisualisation),
            typeof(EzVisualizerPlaylist),
        };

        private static readonly HashSet<Type> scene_specific = Union(results, music_player);

        public static bool IsAllowed(EzEditorSceneType scene, Type type) => scene switch
        {
            EzEditorSceneType.ResultsHud => results.Contains(type),
            EzEditorSceneType.MusicPlayerHud => music_player.Contains(type),
            _ => !scene_specific.Contains(type),
        };

        private static HashSet<Type> Union(HashSet<Type> first, HashSet<Type> second)
        {
            var combined = new HashSet<Type>(first);
            combined.UnionWith(second);
            return combined;
        }
    }
}
