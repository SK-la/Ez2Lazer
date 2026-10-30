// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Edit.Components;
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
    /// Ez 皮肤编辑器盖层。用 <see cref="ScalingContainer.SetCustomRect"/> 把当前屏缩进游玩区，
    /// 不 Push，故回放中途可继续。背景与 chrome 由本层绘制，当前屏不换背景栈。
    /// </summary>
    public partial class EzSkinEditorOverlay : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        private readonly ScalingContainer scalingContainer;

        private OsuScreen? sessionTarget;
        private EzSkinEditorSceneCoordinator? sceneCoordinator;

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

        protected override bool BlockNonPositionalInput => true;

        public EzSkinEditorOverlay(ScalingContainer scalingContainer)
        {
            this.scalingContainer = scalingContainer;
            RelativeSizeAxes = Axes.Both;
            AddLayout(drawSizeLayout = new LayoutValue(Invalidation.DrawSize));
        }

        public EzSkinEditorScreen? Workspace { get; private set; }

        public void Toggle()
        {
            if (State.Value == Visibility.Visible)
                Hide();
            else
                Show();
        }

        protected override void PopIn()
        {
            sessionTarget = game.ScreenStack.CurrentScreen as OsuScreen;

            // Keep the global (main menu) background alive behind plugged screens: stop screens from
            // pushing their own beatmap background, and drop the one already on the stack.
            EzSkinEditorActiveState.SetActive(true);
            Schedule(() => sessionTarget?.DetachOwnedBackground());

            if (Workspace == null)
            {
                Workspace = new EzSkinEditorScreen
                {
                    RequestClose = Hide,
                    OpenOfficialHudEditor = openOfficialHudEditor,
                    BeatmapPreviewRequested = PresentBeatmapForRuleset,
                };
                LoadComponentAsync(Workspace, loaded =>
                {
                    if (Workspace != loaded)
                        return;

                    AddInternal(loaded);

                    loaded.Activate();
                    initializeSceneCoordinator(loaded);
                });
            }
            else
            {
                Workspace.Activate();
                initializeSceneCoordinator(Workspace);
            }

            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            // Closing the editor only hides editor chrome; the last edited scene keeps running.
            sceneCoordinator?.Close();
            sceneCoordinator = null;

            EzSkinEditorActiveState.SetActive(false);

            Workspace?.Deactivate();
            this.FadeOut(200, Easing.OutQuint);
        }

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

        private bool canPresentScene(EzSkinEditorSceneType scene) => scene != EzSkinEditorSceneType.SkinIni || EzSkinIniSupport.IsSupported(skinManager.CurrentSkinInfo.Value);

        private void initializeSceneCoordinator(EzSkinEditorScreen workspace)
        {
            sceneCoordinator = new EzSkinEditorSceneCoordinator(scalingContainer, workspace, layoutLayer, updateScreenSizing, canPresentScene, () => game.ScreenStack.CurrentScreen, enterSongSelect, enterGameplay);
            workspace.SceneApplied = scene => sceneCoordinator?.Present(scene, game.ScreenStack.CurrentScreen as OsuScreen);
            workspace.CanPresentScene = canPresentScene;
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
                    return;

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
        private void PresentBeatmapForRuleset(RulesetInfo rulesetInfo, WorkingBeatmap workingBeatmap)
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

        private void updateScreenSizing()
        {
            if (State.Value != Visibility.Visible || Workspace?.IsLoaded != true)
                return;

            const float padding = 10;
            float relativeRailWidth = EzSkinEditorSidebarGroupRail.WIDTH / DrawWidth;
            float relativeToolbarHeight = (EzSkinEditorSceneBar.HEIGHT + global::osu.Game.Overlays.SkinEditor.SkinEditor.MENU_HEIGHT + padding) / DrawHeight;

            float relativeWidth = 1 - relativeRailWidth;
            float spaceHeight = 1f - relativeToolbarHeight - padding / DrawHeight;
            float targetHeight = relativeWidth;
            float offsetY;

            if (targetHeight > spaceHeight)
            {
                targetHeight = spaceHeight;
                offsetY = 0f;
            }
            else
            {
                offsetY = (spaceHeight - targetHeight) / 2f;
            }

            scalingContainer.SetCustomRect(new RectangleF(0, relativeToolbarHeight + offsetY, relativeWidth, targetHeight), true);
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
            // 关掉 Ez 盖层（恢复插拔屏缩放），再拉起官方 HUD 皮肤编辑器接续编辑。
            Hide();

            if (officialSkinEditor == null)
                return;

            if (officialSkinEditor.State.Value != Visibility.Visible)
                officialSkinEditor.Show();

            officialSkinEditor.PresentGameplay();
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
