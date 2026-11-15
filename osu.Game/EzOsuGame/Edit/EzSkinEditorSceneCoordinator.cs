// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Screens;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Screens.Visualizer;
using osu.Game.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
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
        private readonly Action enterResults;
        private readonly Action enterMusicPlayer;

        private readonly Container gameplayHost = new Container { RelativeSizeAxes = Axes.Both };
        private EzEditorSceneType desiredScene;
        private Player? boundPlayer;
        private SongSelect? boundSongSelect;
        private EzSkinEditorPreviewManiaScreen? boundFullMapPreview;
        private EzSkinEditorBeatmapComposePreviewScreen? boundBeatmapCompose;
        private ResultsScreen? boundResults;
        private EzVisualizerScreen? boundMusicPlayer;

        private Drawable? hiddenStoryboard;
        private float previousStoryboardAlpha;

        private bool sceneLoadingActive;
        private int scenePlugStallFrames;
        private int presentSettleFrames;

        private const int max_scene_plug_stall_frames = 600;

        /// <summary>
        /// 游玩首帧展示后再露出一帧，让舞台材质（含 GrooveLight 混合与单帧呼吸）完成布局与混合。
        /// </summary>
        private const int gameplay_present_settle_frames = 12;

        public EzSkinEditorSceneCoordinator(ScalingContainer scalingContainer, EzSkinEditorScreen workspace, EzLayoutLayer? layoutLayer, Action updateScreenSizing, Func<EzEditorSceneType, bool> sceneIsAvailable, Func<IScreen?> getCurrentScreen, Action enterSongSelect, Action enterGameplay, Action enterFullMapPreview, Action enterBeatmapCompose, Action enterResults, Action enterMusicPlayer)
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
            this.enterResults = enterResults;
            this.enterMusicPlayer = enterMusicPlayer;
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
                switch (scene)
                {
                    case EzEditorSceneType.BeatmapPreview:
                        presentFullMapPreview();
                        break;

                    case EzEditorSceneType.ResultsHud:
                        presentResults();
                        break;

                    case EzEditorSceneType.MusicPlayerHud:
                        presentMusicPlayer();
                        break;

                    default:
                        presentSongSelect();
                        break;
                }
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
            updateScenePlugLoading(current);

            if (EzSkinEditorSceneRegistry.GroupOf(desiredScene) == EzEditorSceneGroup.SongSelect)
            {
                switch (desiredScene)
                {
                    case EzEditorSceneType.BeatmapPreview:
                        if (current is EzSkinEditorPreviewManiaScreen { IsLoaded: true } fullMapPreview && !ReferenceEquals(boundFullMapPreview, fullMapPreview))
                            bindFullMapPreview(fullMapPreview);
                        return;

                    case EzEditorSceneType.ResultsHud:
                        if (current is ResultsScreen { IsLoaded: true } results && !ReferenceEquals(boundResults, results))
                            bindResults(results);
                        return;

                    case EzEditorSceneType.MusicPlayerHud:
                        if (current is EzVisualizerScreen { IsLoaded: true } musicPlayer && !ReferenceEquals(boundMusicPlayer, musicPlayer))
                            bindMusicPlayer(musicPlayer);
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
            forceEndSceneLoading();

            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundResults = null;
            boundMusicPlayer = null;
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
                clearBoundScreens();
                workspace.BindHudToTarget(null, layoutLayer);
                beginSceneLoading();
                enterGameplay();
            }
        }

        private void bindGameplay(Player player)
        {
            boundPlayer = player;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundResults = null;
            boundMusicPlayer = null;

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
            tryCompleteSceneLoading(player);
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
            boundResults = null;
            boundMusicPlayer = null;
            workspace.SetSceneHost(null);
            updateScreenSizing();

            boundSongSelect = null;
            workspace.BindHudToTarget(null, layoutLayer);
            beginSceneLoading();
            enterSongSelect();
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
            boundResults = null;
            boundMusicPlayer = null;
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
            beginSceneLoading();
            enterFullMapPreview();
        }

        private void presentResults()
        {
            restorePlayerChrome();
            clearBoundScreens();
            workspace.SetSceneHost(null);
            workspace.BindHudToTarget(null, layoutLayer);
            updateScreenSizing();

            if (getCurrentScreen() is ResultsScreen { IsLoaded: true } existing)
            {
                bindResults(existing);
                return;
            }

            beginSceneLoading();
            enterResults();
        }

        private void presentMusicPlayer()
        {
            restorePlayerChrome();
            clearBoundScreens();
            workspace.SetSceneHost(null);
            workspace.BindHudToTarget(null, layoutLayer);
            updateScreenSizing();

            if (getCurrentScreen() is EzVisualizerScreen { IsLoaded: true } existing)
            {
                bindMusicPlayer(existing);
                return;
            }

            beginSceneLoading();
            enterMusicPlayer();
        }

        private void presentBeatmapCompose()
        {
            restorePlayerChrome();
            boundPlayer = null;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundResults = null;
            boundMusicPlayer = null;
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
            beginSceneLoading();
            enterBeatmapCompose();
        }

        private void bindBeatmapCompose(EzSkinEditorBeatmapComposePreviewScreen composeScreen)
        {
            boundBeatmapCompose = composeScreen;
            updateScreenSizing();
            tryCompleteSceneLoading(composeScreen);
        }

        private void bindFullMapPreview(EzSkinEditorPreviewManiaScreen maniaScreen)
        {
            boundFullMapPreview = maniaScreen;
            updateScreenSizing();
            tryCompleteSceneLoading(maniaScreen);
        }

        private void bindSongSelect(SongSelect songSelect)
        {
            boundSongSelect = songSelect;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundResults = null;
            boundMusicPlayer = null;

            // The skin layer HUD is only reached for temporary visibility; only the Ez layout
            // container contributes editable targets.
            songSelect.SuspendForSkinEditor();
            workspace.BindHudToTarget(songSelect, layoutLayer);
            updateScreenSizing();
            tryCompleteSceneLoading(songSelect);
        }

        private void bindResults(ResultsScreen results)
        {
            boundResults = results;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundMusicPlayer = null;
            workspace.BindHudToTarget(results, layoutLayer);
            updateScreenSizing();
            tryCompleteSceneLoading(results);
        }

        private void bindMusicPlayer(EzVisualizerScreen musicPlayer)
        {
            boundMusicPlayer = musicPlayer;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundResults = null;
            workspace.BindHudToTarget(musicPlayer, layoutLayer);
            updateScreenSizing();
            tryCompleteSceneLoading(musicPlayer);
        }

        private void clearBoundScreens()
        {
            boundPlayer = null;
            boundSongSelect = null;
            boundFullMapPreview = null;
            boundBeatmapCompose = null;
            boundResults = null;
            boundMusicPlayer = null;
        }

        private void restorePlayerChrome()
        {
            if (hiddenStoryboard != null)
            {
                hiddenStoryboard.Alpha = previousStoryboardAlpha;
                hiddenStoryboard = null;
            }
        }

        private void beginSceneLoading()
        {
            scenePlugStallFrames = 0;
            presentSettleFrames = 0;

            if (sceneLoadingActive)
                return;

            sceneLoadingActive = true;
            workspace.ShowSceneLoading();
        }

        private void endSceneLoading()
        {
            scenePlugStallFrames = 0;
            presentSettleFrames = 0;

            if (!sceneLoadingActive)
                return;

            sceneLoadingActive = false;
            workspace.HideSceneLoading();
        }

        private void forceEndSceneLoading() => endSceneLoading();

        private void updateScenePlugLoading(OsuScreen? current)
        {
            if (!sceneLoadingActive)
                return;

            if (tryCompleteSceneLoading(current))
                return;

            scenePlugStallFrames++;

            if (scenePlugStallFrames < max_scene_plug_stall_frames)
                return;

            forceEndSceneLoading();
        }

        private bool tryCompleteSceneLoading(OsuScreen? current)
        {
            if (!isBoundForDesiredScene())
                return false;

            if (!hasPresentSettled(current))
                return false;

            endSceneLoading();
            return true;
        }

        private bool hasPresentSettled(OsuScreen? current)
        {
            if (!isScenePresentReady(desiredScene, current))
            {
                presentSettleFrames = 0;
                return false;
            }

            if (desiredScene == EzEditorSceneType.GameplayHud)
            {
                presentSettleFrames++;
                return presentSettleFrames >= gameplay_present_settle_frames;
            }

            return true;
        }

        private static bool isScenePresentReady(EzEditorSceneType scene, OsuScreen? screen)
        {
            if (screen == null)
                return false;

            return scene switch
            {
                EzEditorSceneType.GameplayHud => screen is Player player && player.IsCurrentScreen() && player.IsReadyForSkinEditorScenePresent(),
                EzEditorSceneType.SongSelectHud => screen is SongSelect { IsLoaded: true, CarouselItemsPresented: true },
                _ => screen.IsLoaded,
            };
        }

        private bool isBoundForDesiredScene() =>
            desiredScene switch
            {
                EzEditorSceneType.GameplayHud => boundPlayer != null,
                EzEditorSceneType.SongSelectHud => boundSongSelect != null,
                EzEditorSceneType.BeatmapPreview => boundFullMapPreview != null,
                EzEditorSceneType.ResultsHud => boundResults != null,
                EzEditorSceneType.MusicPlayerHud => boundMusicPlayer != null,
                EzEditorSceneType.EditorColouring => boundBeatmapCompose != null,
                _ => true,
            };
    }
}
