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
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
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

        private OsuScreenStack? songSelectHost;
        private SoloSongSelect? pluggedSongSelect;

        private OsuScreen? lastTargetScreen;
        private float previousTargetAlpha = 1;
        private bool targetScreenHidden;
        private Drawable? hiddenStoryboard;
        private float previousStoryboardAlpha = 1;
        private bool beatmapBackgroundHidden;
        private bool waitingForGameplay;

        private readonly LayoutValue drawSizeLayout;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private EzLayoutLayer? layoutLayer { get; set; }

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
            lastTargetScreen = game.ScreenStack.CurrentScreen as OsuScreen;
            waitingForGameplay = lastTargetScreen is not Player;

            if (waitingForGameplay)
                Schedule(startGameplayForEditing);

            if (Workspace == null)
            {
                Workspace = new EzSkinEditorScreen
                {
                    RequestClose = Hide,
                    OpenOfficialHudEditor = openOfficialHudEditor,
                    SceneChanged = onWorkspaceSceneChanged,
                };
                LoadComponentAsync(Workspace, loaded =>
                {
                    if (Workspace != loaded)
                        return;

                    AddInternal(loaded);

                    if (lastTargetScreen is not Player)
                        loaded.CurrentScene.Value = EzSkinEditorSceneType.Appearance;

                    loaded.Activate();
                    onWorkspaceSceneChanged(loaded.CurrentScene.Value);
                });
            }
            else
            {
                if (lastTargetScreen is not Player)
                    Workspace.CurrentScene.Value = EzSkinEditorSceneType.Appearance;

                Workspace.Activate();
                onWorkspaceSceneChanged(Workspace.CurrentScene.Value);
            }

            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            waitingForGameplay = false;
            detachSongSelect();
            restoreNonPlayerTarget();
            restoreTargetPresentation();
            scalingContainer.SetCustomRect(null);
            Workspace?.Deactivate();
            this.FadeOut(200, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            if (State.Value != Visibility.Visible)
                return;

            if (waitingForGameplay && game.ScreenStack.CurrentScreen is Player player && player.IsLoaded)
            {
                waitingForGameplay = false;
                lastTargetScreen = player;
                Workspace?.BindHudToTarget(player, layoutLayer);
                applyTargetPresentation();
            }

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

        private void onWorkspaceSceneChanged(EzSkinEditorSceneType scene)
        {
            if (scene == EzSkinEditorSceneType.SongSelect)
                attachSongSelect();
            else
            {
                detachSongSelect();
                restoreNonPlayerTarget();
                applyTargetPresentation();
            }
        }

        private void attachSongSelect()
        {
            scalingContainer.SetCustomRect(null);
            restorePlayerChrome();
            hideNonPlayerTarget();
            Workspace!.BindHudToTarget(null, layoutLayer);

            songSelectHost ??= new OsuScreenStack
            {
                RelativeSizeAxes = Axes.Both,
            };

            if (songSelectHost.CurrentScreen == null)
            {
                pluggedSongSelect = new SoloSongSelect();
                songSelectHost.Push(pluggedSongSelect);
            }

            if (songSelectHost.Parent == null)
                Workspace!.SetPluggedSongSelectHost(songSelectHost);

            songSelectHost.Alpha = 1;
        }

        private void detachSongSelect()
        {
            songSelectHost?.Alpha = 0;
        }

        private void hideNonPlayerTarget()
        {
            if (lastTargetScreen == null || lastTargetScreen is Player || targetScreenHidden)
                return;

            previousTargetAlpha = lastTargetScreen.Alpha;
            lastTargetScreen.Alpha = 0;
            targetScreenHidden = true;
        }

        private void restoreNonPlayerTarget()
        {
            if (!targetScreenHidden || lastTargetScreen == null)
                return;

            lastTargetScreen.Alpha = previousTargetAlpha;
            targetScreenHidden = false;
        }

        private void applyTargetPresentation()
        {
            if (Workspace?.CurrentScene.Value == EzSkinEditorSceneType.SongSelect)
                return;

            updateScreenSizing();
            hidePlayerChrome();
            Workspace?.BindHudToTarget(lastTargetScreen, layoutLayer);
        }

        private void hidePlayerChrome()
        {
            if (lastTargetScreen is not Player player || !player.IsLoaded)
                return;

            if (player.DimmableStoryboard != null && hiddenStoryboard == null)
            {
                hiddenStoryboard = player.DimmableStoryboard;
                previousStoryboardAlpha = hiddenStoryboard.Alpha;
                hiddenStoryboard.Alpha = 0;
            }

            if (!beatmapBackgroundHidden)
            {
                player.ApplyToBackground(b => b.Hide());
                beatmapBackgroundHidden = true;
            }
        }

        private void restorePlayerChrome() => restoreTargetPresentation();

        private void restoreTargetPresentation()
        {
            if (hiddenStoryboard != null)
            {
                hiddenStoryboard.Alpha = previousStoryboardAlpha;
                hiddenStoryboard = null;
            }

            if (beatmapBackgroundHidden && lastTargetScreen is Player player)
            {
                player.ApplyToBackground(b => b.Show());
                beatmapBackgroundHidden = false;
            }
        }

        private void updateScreenSizing()
        {
            if (State.Value != Visibility.Visible || Workspace?.IsLoaded != true)
                return;

            if (Workspace.CurrentScene.Value == EzSkinEditorSceneType.SongSelect)
            {
                scalingContainer.SetCustomRect(null);
                return;
            }

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

        private void startGameplayForEditing()
        {
            performer?.PerformFromScreen(screen =>
            {
                if (!waitingForGameplay || State.Value != Visibility.Visible || screen is not OsuScreen osuScreen || screen is Player)
                    return;

                if (!tryUseBeatmapForActiveRuleset())
                    return;

                if (ruleset.Value?.Available != true)
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

                osuScreen.Push(new EzEditorEndlessPlayer(autoplay.CreateScoreFromReplayData));
            }, new[] { typeof(Player), typeof(SoloSongSelect) });
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
            Hide();
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
