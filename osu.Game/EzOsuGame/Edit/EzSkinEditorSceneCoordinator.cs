// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Screens;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Presents Ez editor scenes. Song select, gameplay, preview and compose are real screens.
    /// Closing the editor drops its own bindings and leaves the screen that was being edited.
    /// </summary>
    internal sealed class EzSkinEditorSceneCoordinator
    {
        private readonly ScalingContainer scalingContainer;
        private readonly EzSkinEditorScreen workspace;
        private readonly EzLayoutLayer? layoutLayer;
        private readonly Action updateScreenSizing;
        private readonly Func<EzEditorSceneType, bool> sceneIsAvailable;
        private readonly Func<IScreen?> getCurrentScreen;
        private readonly Action enterSongSelect;
        private readonly Action enterGameplay;
        private readonly Action enterFullMapPreview;
        private readonly Action enterBeatmapCompose;

        private readonly Container gameplayHost = new Container { RelativeSizeAxes = Axes.Both };
        private EzEditorSceneType desiredScene;
        private Player? boundPlayer;
        private SongSelect? boundSongSelect;
        private EzSkinEditorPreviewManiaScreen? boundFullMapPreview;
        private EzSkinEditorBeatmapComposePreviewScreen? boundBeatmapCompose;

        private Drawable? hiddenStoryboard;
        private float previousStoryboardAlpha;

        public EzSkinEditorSceneCoordinator(ScalingContainer scalingContainer, EzSkinEditorScreen workspace, EzLayoutLayer? layoutLayer, Action updateScreenSizing, Func<EzEditorSceneType, bool> sceneIsAvailable, Func<IScreen?> getCurrentScreen, Action enterSongSelect, Action enterGameplay, Action enterFullMapPreview, Action enterBeatmapCompose)
        {
            this.scalingContainer = scalingContainer;
            this.workspace = workspace;
            this.layoutLayer = layoutLayer;
            this.updateScreenSizing = updateScreenSizing;
            this.sceneIsAvailable = sceneIsAvailable;
            this.getCurrentScreen = getCurrentScreen;
            this.enterSongSelect = enterSongSelect;
            this.enterGameplay = enterGameplay;
            this.enterFullMapPreview = enterFullMapPreview;
            this.enterBeatmapCompose = enterBeatmapCompose;
            desiredScene = workspace.CurrentScene.Value;
        }

        public void BeginSession(OsuScreen? target)
        {
            desiredScene = EzSkinEditorSceneRegistry.ResolveCurrentScene(target);
            workspace.ApplyCoordinatedScene(desiredScene);
            Present(desiredScene, target);
        }

        public void Present(EzEditorSceneType scene, OsuScreen? target)
        {
            if (!sceneIsAvailable(scene))
                return;

            desiredScene = scene;

            if (EzSkinEditorSceneRegistry.GroupOf(scene) == EzEditorSceneGroup.SongSelect)
            {
                if (scene == EzEditorSceneType.BeatmapPreview)
                    presentFullMapPreview();
                else
                    presentSongSelect();
            }
            else if (EzSkinEditorSceneRegistry.GroupOf(scene) == EzEditorSceneGroup.BeatmapEditor)
                presentBeatmapCompose();
            else
                presentGameplay(target as Player);
        }

        /// <summary>
        /// Binds the editor HUD once a real gameplay screen becomes current. Called every frame by
        /// the overlay so a scene screen pushed asynchronously is picked up after it loads.
        /// </summary>
        public void Tick(OsuScreen? current)
        {
            if (EzSkinEditorSceneRegistry.GroupOf(desiredScene) == EzEditorSceneGroup.SongSelect)
            {
                if (desiredScene == EzEditorSceneType.BeatmapPreview)
                {
                    if (current is EzSkinEditorPreviewManiaScreen { IsLoaded: true } fullMapPreview && !ReferenceEquals(boundFullMapPreview, fullMapPreview))
                        bindFullMapPreview(fullMapPreview);

                    return;
                }

                if (current is SongSelect songSelect && songSelect.IsLoaded && !ReferenceEquals(boundSongSelect, songSelect))
                    bindSongSelect(songSelect);

                return;
            }

            if (EzSkinEditorSceneRegistry.GroupOf(desiredScene) == EzEditorSceneGroup.BeatmapEditor)
            {
                if (current is EzSkinEditorBeatmapComposePreviewScreen { IsLoaded: true } compose && !ReferenceEquals(boundBeatmapCompose, compose))
                    bindBeatmapCompose(compose);

                return;
            }

            if (current is Player player && player.IsLoaded && !ReferenceEquals(boundPlayer, player))
                bindGameplay(player);
        }

        /// <summary>
        /// Drops editor bindings and the scene rect. The screen being edited stays current.
        /// </summary>
        public void Close()
        {
            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
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
                boundFullMapPreview = null;
                boundBeatmapCompose = null;
                workspace.BindHudToTarget(null, layoutLayer);
                enterGameplay();
            }
        }

        private void bindGameplay(Player player)
        {
            boundPlayer = player;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;

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
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
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

        /// <summary>
        /// 谱面预览：把选歌界面退出，再把全谱预览屏插到屏幕栈。屏本身按编辑器给的矩形自适应缩放，
        /// 编辑器不再绑 HUD（该场景没有可编辑 HUD）。
        /// </summary>
        private void presentFullMapPreview()
        {
            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            workspace.SetSceneHost(null);
            workspace.BindHudToTarget(null, layoutLayer);
            updateScreenSizing();

            workspace.SyncPreviewStateFromGlobalBeatmap();

            if (getCurrentScreen() is EzSkinEditorPreviewManiaScreen { IsLoaded: true } existing)
            {
                // 已经在预览屏上：只换谱面源，不重插屏幕。
                existing.SetSource(workspace.PreviewState.PreviewBeatmap.Value, workspace.PreviewState.Ruleset.Value);
                bindFullMapPreview(existing);
                return;
            }

            boundFullMapPreview = null;
            enterFullMapPreview();
        }

        private void presentBeatmapCompose()
        {
            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            boundFullMapPreview = null;
            workspace.SetSceneHost(null);
            workspace.BindHudToTarget(null, layoutLayer);
            updateScreenSizing();

            workspace.SyncPreviewStateFromGlobalBeatmap();

            if (getCurrentScreen() is EzSkinEditorBeatmapComposePreviewScreen { IsLoaded: true } existing)
            {
                existing.SetSource(workspace.PreviewState.PreviewBeatmap.Value, workspace.PreviewState.Ruleset.Value);
                bindBeatmapCompose(existing);
                return;
            }

            boundBeatmapCompose = null;
            enterBeatmapCompose();
        }

        private void bindBeatmapCompose(EzSkinEditorBeatmapComposePreviewScreen composeScreen)
        {
            boundBeatmapCompose = composeScreen;
            updateScreenSizing();
        }

        private void bindFullMapPreview(EzSkinEditorPreviewManiaScreen maniaScreen)
        {
            boundFullMapPreview = maniaScreen;
            updateScreenSizing();
        }

        private void bindSongSelect(SongSelect songSelect)
        {
            boundSongSelect = songSelect;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;

            // The skin layer HUD is only reached for temporary visibility; only the Ez layout
            // container contributes editable targets.
            songSelect.SuspendForSkinEditor();
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
