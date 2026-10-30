// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Presents Ez editor scenes. Non-song-select scenes keep the embedded gameplay viewport; the
    /// song select scene is a real screen on the game stack so its selection outlives the editor.
    /// Closing the editor hides chrome only and leaves the last edited scene running.
    /// </summary>
    internal sealed class EzSkinEditorSceneCoordinator
    {
        private readonly ScalingContainer scalingContainer;
        private readonly EzSkinEditorScreen workspace;
        private readonly EzLayoutLayer? layoutLayer;
        private readonly Action updateScreenSizing;
        private readonly Func<EzSkinEditorSceneType, bool> sceneIsAvailable;
        private readonly Func<IScreen?> getCurrentScreen;
        private readonly Action enterSongSelect;
        private readonly Action enterGameplay;

        private readonly Container gameplayHost = new Container { RelativeSizeAxes = Axes.Both };
        private EzSkinEditorSceneType desiredScene;
        private Player? boundPlayer;
        private SongSelect? boundSongSelect;

        private Drawable? hiddenStoryboard;
        private float previousStoryboardAlpha;

        public EzSkinEditorSceneCoordinator(ScalingContainer scalingContainer, EzSkinEditorScreen workspace, EzLayoutLayer? layoutLayer, Action updateScreenSizing, Func<EzSkinEditorSceneType, bool> sceneIsAvailable, Func<IScreen?> getCurrentScreen, Action enterSongSelect, Action enterGameplay)
        {
            this.scalingContainer = scalingContainer;
            this.workspace = workspace;
            this.layoutLayer = layoutLayer;
            this.updateScreenSizing = updateScreenSizing;
            this.sceneIsAvailable = sceneIsAvailable;
            this.getCurrentScreen = getCurrentScreen;
            this.enterSongSelect = enterSongSelect;
            this.enterGameplay = enterGameplay;
            desiredScene = workspace.CurrentScene.Value;
        }

        public void BeginSession(OsuScreen? target)
        {
            desiredScene = EzSkinEditorSceneRegistry.ResolveCurrentScene(target);
            workspace.ApplyCoordinatedScene(desiredScene);
            Present(desiredScene, target);
        }

        public void Present(EzSkinEditorSceneType scene, OsuScreen? target)
        {
            if (!sceneIsAvailable(scene))
                return;

            desiredScene = scene;

            if (scene == EzSkinEditorSceneType.SongSelect)
                presentSongSelect();
            else
                presentGameplay(target as Player);
        }

        /// <summary>
        /// Binds the editor HUD once a real gameplay screen becomes current. Called every frame by
        /// the overlay so a scene screen pushed asynchronously is picked up after it loads.
        /// </summary>
        public void Tick(OsuScreen? current)
        {
            if (desiredScene == EzSkinEditorSceneType.SongSelect)
            {
                if (current is SongSelect songSelect && songSelect.IsLoaded && !ReferenceEquals(boundSongSelect, songSelect))
                    bindSongSelect(songSelect);

                return;
            }

            if (current is Player player && player.IsLoaded && !ReferenceEquals(boundPlayer, player))
                bindGameplay(player);
        }

        /// <summary>
        /// Hides editor chrome only. The last edited scene keeps running: no screen is exited and
        /// the editor does not return to the screen it was opened from.
        /// </summary>
        public void Close()
        {
            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            workspace.SetSceneHost(null);
            scalingContainer.SetCustomRect(null);
        }

        private void presentGameplay(Player? player)
        {
            workspace.SetSceneHost(gameplayHost);

            // A live gameplay screen is edited in place; otherwise the host starts the editor's
            // own endless gameplay and binding happens once it becomes current.
            if (player is { IsLoaded: true } && player.IsCurrentScreen())
                bindGameplay(player);
            else
            {
                restorePlayerChrome();
                boundPlayer = null;
                boundSongSelect = null;
                workspace.BindHudToTarget(null, layoutLayer);
                enterGameplay();
            }
        }

        private void bindGameplay(Player player)
        {
            boundPlayer = player;
            boundSongSelect = null;

            // Gameplay may have pushed its own beatmap background before the editor opened.
            player.DetachOwnedBackground();

            if (player.DimmableStoryboard != null && hiddenStoryboard == null)
            {
                hiddenStoryboard = player.DimmableStoryboard;
                previousStoryboardAlpha = hiddenStoryboard.Alpha;
                hiddenStoryboard.Alpha = 0;
            }

            workspace.BindHudToTarget(player, layoutLayer);
            updateScreenSizing();
        }

        private void presentSongSelect()
        {
            // A real song select screen is used so the selection outlives the editor overlay. Any
            // running gameplay is ended first (via the screen runner), so song select binds against
            // the game's own mods state instead of a live player's leased bindables.
            restorePlayerChrome();
            boundPlayer = null;
            workspace.SetSceneHost(null);
            updateScreenSizing();

            if (getCurrentScreen() is SongSelect songSelect && songSelect.IsLoaded)
                bindSongSelect(songSelect);
            else
            {
                boundSongSelect = null;
                workspace.BindHudToTarget(null, layoutLayer);
                enterSongSelect();
            }
        }

        private void bindSongSelect(SongSelect songSelect)
        {
            boundSongSelect = songSelect;

            // The skin layer HUD is only reached for temporary visibility; only the Ez layout
            // container contributes editable targets.
            workspace.BindHudToTarget(songSelect, layoutLayer);
            updateScreenSizing();
        }

        private void restorePlayerChrome()
        {
            if (hiddenStoryboard != null)
            {
                hiddenStoryboard.Alpha = previousStoryboardAlpha;
                hiddenStoryboard = null;
            }
        }
    }
}
