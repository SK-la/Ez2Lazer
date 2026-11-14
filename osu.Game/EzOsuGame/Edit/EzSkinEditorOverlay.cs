// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Screens;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Ez 皮肤编辑器盖层。用 <see cref="ScalingContainer.SetCustomRect"/> 把当前屏缩进游玩区。
    /// 关闭时留在正在编辑的那一屏，只卸掉编辑器盖上去的背景和挂起状态。
    /// </summary>
    public partial class EzSkinEditorOverlay : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        private readonly ScalingContainer scalingContainer;

        private OsuScreen? sessionTarget;
        private EzSkinEditorSceneCoordinator? sceneCoordinator;

        // 当前已订阅 ReservedWidth 的工作区；关层时要解绑。
        private EzSkinEditorScreen? reservedWidthBound;

        private readonly LayoutValue drawSizeLayout;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private EzLayoutLayer? layoutLayer { get; set; }

        [Resolved(canBeNull: true)]
        private SkinEditorOverlay? officialSkinEditor { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        private EzSkinEditorBeatmapPicker? beatmapPicker;

        private readonly Bindable<bool> beatmapSkins = new Bindable<bool>();
        private LeasedBindable<bool>? leasedBeatmapSkins;

        private readonly Bindable<HUDVisibilityMode> configVisibilityMode = new Bindable<HUDVisibilityMode>();
        private LeasedBindable<HUDVisibilityMode>? leasedVisibilityMode;

        protected override bool BlockNonPositionalInput => true;

        public EzSkinEditorOverlay(ScalingContainer scalingContainer)
        {
            this.scalingContainer = scalingContainer;
            RelativeSizeAxes = Axes.Both;
            AddLayout(drawSizeLayout = new LayoutValue(Invalidation.DrawSize));
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.BeatmapSkins, beatmapSkins);
            config.BindWith(OsuSetting.HUDVisibilityMode, configVisibilityMode);
        }

        public EzSkinEditorScreen? Workspace { get; private set; }

        public void Toggle()
        {
            if (State.Value == Visibility.Visible)
            {
                Hide();
                return;
            }

            void openEz()
            {
                try
                {
                    Show();
                }
                finally
                {
                    EzSkinEditorHostScope.EndTransition();
                }
            }

            if (officialSkinEditor?.State.Value == Visibility.Visible)
            {
                EzSkinEditorHostScope.BeginTransition();
                officialSkinEditor.RequestEndEditorSession(openEz);
                return;
            }

            Show();
        }

        protected override void PopIn()
        {
            EzSkinEditorHostScope.Enter(EzSkinEditorHostScope.Host.Ez);
            sessionTarget = game.ScreenStack.CurrentScreen as OsuScreen;
            overrideSkinEditorRelevantSettings();

            // Keep the global (main menu) background alive behind plugged screens: stop screens from
            // pushing their own beatmap background, and drop the one already on the stack.
            EzSkinEditorActiveState.SetActive(true);

            if (sessionTarget is SongSelect songSelect)
                songSelect.SuspendForSkinEditor();
            else
            {
                Schedule(() =>
                {
                    if (!EzSkinEditorActiveState.Active)
                        return;

                    sessionTarget?.DetachOwnedBackground();
                });
            }

            if (Workspace == null)
            {
                Workspace = new EzSkinEditorScreen
                {
                    RequestClose = Hide,
                    OpenOfficialHudEditor = openOfficialHudEditor,
                    BeatmapPreviewRequested = presentBeatmapForRuleset,
                };
                LoadComponentAsync(Workspace, loaded =>
                {
                    if (Workspace != loaded)
                        return;

                    AddInternal(loaded);

                    loaded.Activate();
                    initializeSceneCoordinator(loaded);
                    applyScreenSizing();
                });
            }
            else
            {
                Workspace.Activate();
                initializeSceneCoordinator(Workspace);
                applyScreenSizing();
            }

            this.FadeIn(200, Easing.OutQuint);
            applyScreenSizing();
        }

        protected override void PopOut()
        {
            restoreSkinEditorRelevantSettings();
            sceneCoordinator?.Close();
            sceneCoordinator = null;

            if (reservedWidthBound != null)
            {
                reservedWidthBound.SidebarReservedWidth.ValueChanged -= onReservedWidthChanged;
                reservedWidthBound = null;
            }

            // 先停用，当前屏的 CreateBackground 才不会继续交回 null。
            EzSkinEditorActiveState.SetActive(false);

            Workspace?.Deactivate();
            this.FadeOut(200, Easing.OutQuint);

            // 不退回打开前的屏。只把正在编辑的这一屏上，编辑器挂起的播放和被换掉的背景接回去。
            if (game.ScreenStack.CurrentScreen is OsuScreen current)
            {
                if (current is SongSelect songSelect)
                    songSelect.ResumeFromSkinEditor();
                else
                    EzSkinEditorActiveState.RestoreBackgroundDraw();

                current.RestoreOwnedBackground();
            }
            else
                EzSkinEditorActiveState.RestoreBackgroundDraw();

            sessionTarget = null;
            EzSkinEditorHostScope.Exit(EzSkinEditorHostScope.Host.Ez);
        }

        private void onReservedWidthChanged(ValueChangedEvent<float> _) => updateScreenSizing();

        protected override void Update()
        {
            base.Update();

            if (State.Value != Visibility.Visible)
                return;

            sceneCoordinator?.Tick(game.ScreenStack.CurrentScreen as OsuScreen);

            if (!drawSizeLayout.IsValid)
            {
                updateScreenSizing();
                drawSizeLayout.Validate();
            }
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.Back || State.Value != Visibility.Visible)
                return false;

            Workspace?.RequestCloseFromUser();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private bool canPresentScene(EzEditorSceneType scene)
        {
            if (scene == EzEditorSceneType.SkinIni)
                return EzSkinIniSupport.IsSupported(skinManager.CurrentSkinInfo.Value);

            return true;
        }

        private void initializeSceneCoordinator(EzSkinEditorScreen workspace)
        {
            sceneCoordinator = new EzSkinEditorSceneCoordinator(scalingContainer, workspace, layoutLayer, updateScreenSizing, canPresentScene, () => game.ScreenStack.CurrentScreen, enterSongSelect, enterGameplay, enterFullMapPreview, enterBeatmapCompose);
            workspace.SceneApplied = scene => sceneCoordinator?.Present(scene, game.ScreenStack.CurrentScreen as OsuScreen);
            workspace.CanPresentScene = canPresentScene;

            // 侧栏固定时挤占场景可用宽度；宽度变化后跟随重算插拔场景矩形。
            workspace.SidebarReservedWidth.ValueChanged += onReservedWidthChanged;
            reservedWidthBound = workspace;

            sceneCoordinator.BeginSession(sessionTarget);
        }

        /// <summary>
        /// Ends whatever is on screen and opens a real song select screen. Editor-owned gameplay is
        /// exited first so song select binds against the game's own mods state.
        /// </summary>
        private void enterSongSelect()
        {
            performer?.PerformFromScreen(screen =>
            {
                if (screen is SongSelect)
                {
                    // Same re-plug path as leaving gameplay: drop expanded preview / carousel state on the old instance.
                    screen.Exit();
                    Schedule(enterSongSelect);
                    return;
                }

                if (screen is Player player)
                {
                    player.ValidForResume = false;
                    player.Exit();
                    Schedule(enterSongSelect);
                    return;
                }

                screen.Push(new SoloSongSelect());
            }, new[] { typeof(SongSelect) });
        }

        /// <summary>
        /// Steps back to the main menu (exiting song select / gameplay) and pushes the editor's
        /// full-map preview screen. The screen renders whatever the editor's preview state holds.
        /// </summary>
        private void enterFullMapPreview()
        {
            performer?.PerformFromScreen(screen =>
            {
                if (screen is EzSkinEditorPreviewManiaScreen || State.Value != Visibility.Visible)
                    return;

                if (Workspace == null)
                    return;

                screen.Push(new EzSkinEditorPreviewManiaScreen(
                    Workspace.PreviewState.PreviewBeatmap.Value,
                    Workspace.PreviewState.Ruleset.Value));
            }, new[] { typeof(MainMenu) });
        }

        private void enterBeatmapCompose()
        {
            performer?.PerformFromScreen(screen =>
            {
                if (screen is EzSkinEditorBeatmapComposePreviewScreen || State.Value != Visibility.Visible)
                    return;

                if (Workspace == null)
                    return;

                screen.Push(new EzSkinEditorBeatmapComposePreviewScreen(
                    Workspace.PreviewState.PreviewBeatmap.Value,
                    Workspace.PreviewState.Ruleset.Value,
                    Workspace.PreviewState.PreviewGridBeatDivisor));
            }, new[] { typeof(MainMenu) });
        }

        /// <summary>
        /// Opens the editor's endless gameplay on the real screen stack.
        /// </summary>
        private void enterGameplay()
        {
            performer?.PerformFromScreen(screen =>
            {
                if (screen is Player || State.Value != Visibility.Visible)
                    return;

                if (!tryUseBeatmapForActiveRuleset() || ruleset.Value?.Available != true)
                    return;

                ModAutoplay? autoplay;

                try
                {
                    autoplay = ruleset.Value.CreateInstance().GetAutoplayMod();
                }
                catch
                {
                    return;
                }

                if (autoplay == null)
                    return;

                IReadOnlyList<Mod> usableMods = mods.Value.Append(autoplay).ToArray();

                if (!ModUtils.CheckCompatibleSet(usableMods, out var invalid))
                    mods.Value = mods.Value.Except(invalid).ToArray();

                screen.Push(new EzEditorEndlessPlayer(autoplay.CreateScoreFromReplayData));
            }, new[] { typeof(Player), typeof(SoloSongSelect), typeof(MainMenu) });
        }

        /// <summary>
        /// Switches the global ruleset and the picked preview beatmap, then rebuilds the editor scene so the
        /// plugged gameplay screen restarts on the newly picked chart.
        /// The visible preview is a real <see cref="Player"/> bound to the global beatmap, so
        /// <see cref="IPerformFromScreenRunner"/> first steps back to song select / main menu (exiting any
        /// running gameplay) before the switch is applied.
        /// </summary>
        private void presentBeatmapForRuleset(RulesetInfo rulesetInfo, WorkingBeatmap workingBeatmap)
        {
            performer?.PerformFromScreen(_ =>
            {
                if (State.Value != Visibility.Visible)
                    return;

                if (ruleset is Bindable<RulesetInfo> writableRuleset)
                    writableRuleset.Value = rulesetInfo;

                if (beatmap is Bindable<WorkingBeatmap> writableBeatmap)
                    writableBeatmap.Value = workingBeatmap;

                musicController.Stop();

                // Re-presents the scene so gameplay restarts on the new chart. The sidebar and HUD layer are
                // not rebuilt: they follow the shared PreviewState and are unaffected by the chart switch.
                Workspace?.RefreshPreviewScene();
            }, new[] { typeof(SoloSongSelect), typeof(MainMenu) });
        }

        private void updateScreenSizing() => applyScreenSizing();

        private void applyScreenSizing()
        {
            if (State.Value != Visibility.Visible)
                return;

            const float toolbar_height = EzSkinEditorSceneBar.HEIGHT + global::osu.Game.Overlays.SkinEditor.SkinEditor.MENU_HEIGHT;
            float leftRailWidth = Workspace?.IsLoaded == true ? Workspace.SceneRailReservedWidth : EzVerticalRail.WIDTH;
            float reservedWidth = Workspace?.IsLoaded == true ? Workspace.SidebarReservedWidth.Value : 0;

            scalingContainer.SetCustomRect(EzSkinEditorSceneSizing.ComputeSceneRect(DrawSize, toolbar_height, leftRailWidth, reservedWidth), true);
        }

        private bool tryUseBeatmapForActiveRuleset()
        {
            if (ruleset.Value is not RulesetInfo active || !active.Available)
                return false;

            if (isPlayableForRuleset(beatmap.Value, active))
                return true;

            beatmapPicker ??= new EzSkinEditorBeatmapPicker(realm, beatmaps);

            if (!beatmapPicker.TryPickRandom(active, out var working) || working is not WorkingBeatmap picked)
                return false;

            if (beatmap is not Bindable<WorkingBeatmap> writable || writable.Disabled)
                return false;

            writable.Value = picked;
            return isPlayableForRuleset(picked, active);
        }

        private static bool isPlayableForRuleset(WorkingBeatmap working, RulesetInfo active) =>
            working is not DummyWorkingBeatmap
            && !working.BeatmapSetInfo.Protected
            && active.Equals(working.BeatmapInfo.Ruleset)
            && working.BeatmapInfo.TotalObjectCount > 0;

        private void openOfficialHudEditor()
        {
            if (Workspace == null)
                return;

            void openOfficialEditor()
            {
                try
                {
                    Hide();

                    if (officialSkinEditor == null)
                        return;

                    if (officialSkinEditor.State.Value != Visibility.Visible)
                        officialSkinEditor.Show();

                    officialSkinEditor.PresentGameplay();
                }
                finally
                {
                    EzSkinEditorHostScope.EndTransition();
                }
            }

            EzSkinEditorHostScope.BeginTransition();
            Workspace.RequestEndSession(openOfficialEditor);
        }

        private void overrideSkinEditorRelevantSettings()
        {
            if (!beatmapSkins.Disabled)
            {
                leasedBeatmapSkins = beatmapSkins.BeginLease(true);
                leasedBeatmapSkins.Value = false;
            }

            leasedVisibilityMode = configVisibilityMode.BeginLease(true);
            leasedVisibilityMode.Value = HUDVisibilityMode.Always;
        }

        private void restoreSkinEditorRelevantSettings()
        {
            leasedBeatmapSkins?.Return();
            leasedBeatmapSkins = null;

            leasedVisibilityMode?.Return();
            leasedVisibilityMode = null;
        }

        private partial class EzEditorEndlessPlayer : ReplayPlayer
        {
            protected override UserActivity? InitialActivity => null;

            public override bool DisallowExternalBeatmapRulesetChanges => true;

            public override bool? AllowGlobalTrackControl => false;

            public EzEditorEndlessPlayer(Func<IBeatmap, IReadOnlyList<Mod>, Score> createScore)
                : base(createScore, new PlayerConfiguration
                {
                    ShowResults = false,
                    AutomaticallySkipIntro = true,
                })
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (!LoadedBeatmapSuccessfully)
                    Scheduler.AddDelayed(this.Exit, 1000);
            }

            protected override void Update()
            {
                base.Update();

                if (LoadedBeatmapSuccessfully && GameplayState.HasPassed)
                    GameplayClockContainer.Seek(0);
            }
        }
    }
}
