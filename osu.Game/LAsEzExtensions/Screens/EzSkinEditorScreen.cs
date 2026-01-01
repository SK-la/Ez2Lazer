// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Users;
using osu.Game.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays.Dialog;

namespace osu.Game.LAsEzExtensions.Screens
{
    /// <summary>
    /// 一个屏幕，用于在用户请求时加载皮肤编辑器以指定目标。
    /// 这也处理目标的缩放/定位调整。
    /// </summary>
    public partial class EzSkinEditorScreen : Screen, IKeyBindingHandler<GlobalAction>
    {
        [Resolved]
        private IPerformFromScreenRunner? performer { get; set; }

        [Cached]
        public readonly EditorClipboard Clipboard = new EditorClipboard();

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private OsuScreen? lastTargetScreen;
        private InvokeOnDisposal? nestedInputManagerDisable;

        private readonly LayoutValue drawSizeLayout;

        // 重新结构化布局的新组件
        private Container mainContainer;
        private Container leftPlaybackContainer;
        private Container centerNoteDisplayContainer;
        private Container rightSettingsContainer;
        private OsuScrollContainer settingsScrollContainer;
        private OsuButton applyButton;
        private DialogOverlay dialogOverlay;

        public EzSkinEditorScreen()
        {
            AddLayout(drawSizeLayout = new LayoutValue(Invalidation.DrawSize));
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            config.BindWith(OsuSetting.BeatmapSkins, beatmapSkins);

            InternalChildren = new Drawable[]
            {
                mainContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // 左侧：虚拟播放场景
                        leftPlaybackContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 200,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        // 中间：note 显示
                        centerNoteDisplayContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 300,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        // 右侧：设置面板
                        rightSettingsContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 250,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                settingsScrollContainer = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Height = 0.9f, // 为应用按钮留出空间
                                },
                                applyButton = new ApplySettingsButton
                                {
                                    Text = "Apply Settings",
                                    RelativeSizeAxes = Axes.X,
                                    Height = 40,
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    Action = ApplySettings,
                                }
                            }
                        }
                    }
                }
            };
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.Back:
                    ShowExitDialog();
                    return true;
            }

            return false;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            globallyDisableBeatmapSkinSetting();

            disableNestedInputManagers();

            // 初始化左侧播放容器，使用虚拟 note
            InitializeLeftPlayback();

            // 初始化中间 note 显示
            InitializeCenterDisplay();

            // 初始化右侧设置面板
            InitializeRightSettings();

            game?.Toolbar.Hide();
            game?.CloseAllOverlays();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            nestedInputManagerDisable?.Dispose();
            nestedInputManagerDisable = null;

            globallyReenableBeatmapSkinSetting();

            if (lastTargetScreen?.HideOverlaysOnEnter != true)
                game?.Toolbar.Show();

            return false;
        }

        public void PopulateSettings()
        {
            // 当调用时初始化屏幕
            InitializeLeftPlayback();
            InitializeCenterDisplay();
            InitializeRightSettings();
        }

        private void InitializeLeftPlayback()
        {
            leftPlaybackContainer.Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new[]
                    {
                        CreateVirtualHoldNote(true),  // 始终命中
                        CreateVirtualHoldNote(false), // 始终miss
                    }
                }
            };
        }

        private Drawable CreateVirtualHoldNote(bool isHitting)
        {
            // 简化的 hold note 占位符
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = isHitting ? Color4.Green : Color4.Red,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = isHitting ? "HIT" : "MISS",
                        Colour = Color4.White,
                    }
                }
            };
        }

        private void InitializeCenterDisplay()
        {
            centerNoteDisplayContainer.Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5), // 5像素间隙
                    Children = new[]
                    {
                        CreateCurrentSkinHoldNote(),
                        CreateEditedHoldNote(),
                    }
                }
            };
        }

        private Drawable CreateCurrentSkinHoldNote()
        {
            // 简化的占位符
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Width = 0.5f,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Blue,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Current Skin\nHold Note",
                        Colour = Color4.White,
                    }
                }
            };
        }

        private Drawable CreateEditedHoldNote()
        {
            // 简化的占位符
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Width = 0.5f,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Purple,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Edited\nHold Note",
                        Colour = Color4.White,
                    }
                }
            };
        }

        private void InitializeRightSettings()
        {
            // 目前，添加一个设置的占位符
            settingsScrollContainer.Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
                Padding = new MarginPadding(10),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Settings will be added here",
                        Colour = Color4.White,
                    }
                }
            };
        }

        private void ApplySettings()
        {
            // TODO: 将设置应用到当前皮肤
            // 目前只是刷新中间显示
            InitializeCenterDisplay();
        }

        private void ShowExitDialog()
        {
            if (dialogOverlay == null)
            {
                // 如果尚未解析，则解析对话框覆盖层
                // 假设它在游戏中可用
                // 为简单起见，我们创建一个简单的对话框
                var dialog = new DialogOverlay();
                AddInternal(dialog);
                dialogOverlay = dialog;
            }

            dialogOverlay.Push(new ConfirmDialog("应用更改到皮肤？", () =>
            {
                ApplySettings();
                this.Exit();
            }, () => this.Exit()));
        }

        public void PresentGameplay() => presentGameplay(false);

        private void presentGameplay(bool attemptedBeatmapSwitch)
        {
            performer?.PerformFromScreen(screen =>
            {
                if (beatmap.Value is DummyWorkingBeatmap)
                {
                    // 假设我们没有好的东西可以播放，直接退出
                    return;
                }

                // 如果我们正在播放介绍，切换到另一个谱面
                if (beatmap.Value.BeatmapSetInfo.Protected)
                {
                    if (!attemptedBeatmapSwitch)
                    {
                        music.NextTrack();
                        Schedule(() => presentGameplay(true));
                    }

                    return;
                }

                if (screen is Player)
                    return;

                // 当前游戏范围内的谱面 + 规则集组合的有效性由歌曲选择强制执行。
                // 如果我们在其他地方，状态未知，可能没有意义，所以强制设置一些有意义的东西。
                // if (screen is not PlaySongSelect)
                //     ruleset.Value = beatmap.Value.BeatmapInfo.Ruleset;
                var replayGeneratingMod = ruleset.Value.CreateInstance().GetAutoplayMod();

                IReadOnlyList<Mod> usableMods = mods.Value;

                if (replayGeneratingMod != null)
                    usableMods = usableMods.Append(replayGeneratingMod).ToArray();

                if (!ModUtils.CheckCompatibleSet(usableMods, out var invalid))
                    mods.Value = mods.Value.Except(invalid).ToArray();

                if (replayGeneratingMod != null)
                    // TODO: 实现游戏呈现
                    return;
            }, new[] { typeof(Player) });
        }

        protected override void Update()
        {
            base.Update();

            if (!drawSizeLayout.IsValid)
            {
                drawSizeLayout.Validate();
            }
        }

        private void updateScreenSizing()
        {
            const float padding = 10;

            float relativeSidebarWidth = 250f / DrawWidth; // Right settings width
            float relativeToolbarHeight = (SkinEditorSceneLibrary.HEIGHT + padding) / DrawHeight;

            var rect = new RectangleF(
                200f / DrawWidth, // Left playback width
                relativeToolbarHeight,
                1 - (200f + 300f + 250f) / DrawWidth, // Center width
                1f - relativeToolbarHeight - padding / DrawHeight);

            // 屏幕不需要 scalingContainer
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// 设置一个新的目标屏幕，用于查找可换肤组件。
        /// </summary>
        public void SetTarget(OsuScreen screen)
        {
            nestedInputManagerDisable?.Dispose();
            nestedInputManagerDisable = null;

            lastTargetScreen = screen;

            // AddOnce with parameter 将确保如果有任何重叠，最新的目标被加载。
            Scheduler.AddOnce(setTarget, screen);
        }

        private void setTarget(OsuScreen? target)
        {
            if (target == null)
                return;

            if (!target.IsLoaded || !IsLoaded)
            {
                Scheduler.AddOnce(setTarget, target);
                return;
            }

            disableNestedInputManagers();
        }

        private void disableNestedInputManagers()
        {
            if (lastTargetScreen == null)
                return;

            var nestedInputManagers = lastTargetScreen.ChildrenOfType<PassThroughInputManager>().Where(manager => manager.UseParentInput).ToArray();
            foreach (var inputManager in nestedInputManagers)
                inputManager.UseParentInput = false;
            nestedInputManagerDisable = new InvokeOnDisposal(() =>
            {
                foreach (var inputManager in nestedInputManagers)
                    inputManager.UseParentInput = true;
            });
        }

        private readonly Bindable<bool> beatmapSkins = new Bindable<bool>();
        private LeasedBindable<bool>? leasedBeatmapSkins;

        private void globallyDisableBeatmapSkinSetting()
        {
            if (beatmapSkins.Disabled)
                return;

            // 皮肤编辑器在谱面皮肤被应用到玩家屏幕时工作不好。
            // 为简单起见，在使用皮肤编辑器时全局禁用该设置。
            //
            // 这会导致皮肤完全重新加载，这看起来很丑。
            // TODO: 调查是否可以在当前谱面未应用谱面皮肤时避免这种情况。
            leasedBeatmapSkins = beatmapSkins.BeginLease(true);
            leasedBeatmapSkins.Value = false;
        }

        private void globallyReenableBeatmapSkinSetting()
        {
            leasedBeatmapSkins?.Return();
            leasedBeatmapSkins = null;
        }

        private partial class ApplySettingsButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? overlayColourProvider, OsuColour colours)
            {
                BackgroundColour = overlayColourProvider?.Background3 ?? colours.Blue3;
                Content.CornerRadius = 5;
            }
        }
    }
}
