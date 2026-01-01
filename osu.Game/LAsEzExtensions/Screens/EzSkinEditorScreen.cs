// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    /// <summary>
    /// 一个屏幕，用于在用户请求时加载皮肤编辑器以指定目标。
    /// 这也处理目标的缩放/定位调整。
    /// </summary>
    public partial class EzSkinEditorScreen : OverlayContainer
    {
        // Must not block the rest of the SkinEditor UI (sidebars/toolbar).
        protected override bool BlockNonPositionalInput => false;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        // 必须这样，否则会构建失败
        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private ISkinEditorVirtualProvider? provider;

        private Container? mainContainer;
        private Container? leftPlaybackContainer;
        private Container? centerNoteDisplayContainer;
        private Container? rightSettingsContainer;
        private OsuScrollContainer? settingsScrollContainer;
        private OsuButton? applyButton;

        public EzSkinEditorScreen()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            // AddLayout(drawSizeLayout = new LayoutValue(Invalidation.DrawSize));
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(200, Easing.OutQuint);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                mainContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Relative, 0.3f),
                                new Dimension(GridSizeMode.Relative, 0.4f),
                                new Dimension(GridSizeMode.Relative, 0.3f),
                            },
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Relative, 1),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    // 左侧：虚拟播放场景
                                    leftPlaybackContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    // 中间：note 显示
                                    centerNoteDisplayContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    // 右侧：设置面板
                                    rightSettingsContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
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
                                    },
                                }
                            }
                        }
                    }
                }
            };

            // 延迟到下一帧调用PopulateSettings，确保所有依赖已加载
            Schedule(PopulateSettings);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // 确保在所有依赖加载完成后调用PopulateSettings
            PopulateSettings();
        }

        public void PopulateSettings()
        {
            // 注意：这里不要创建 HitObjectComposer（会引入编辑器依赖）。
            // 本功能目前只服务 mania + EzPro，通过 registry 解析 provider。
            var currentBeatmap = beatmap.Value.Beatmap;
            var ezProSkin = skinManager.CurrentSkin.Value as EzStyleProSkin ?? new EzStyleProSkin(skinManager);

            // 尽量确保 mania 程序集已加载，以便其在模块初始化时完成 provider 注册。
            try
            {
                Assembly.Load("osu.Game.Rulesets.Mania");
            }
            catch
            {
            }

            provider = createManiaProviderOrNull();

            // 当调用时初始化屏幕
            InitializeLeftPlayback();
            InitializeCenterDisplay();
            InitializeRightSettings();
        }

        private static ISkinEditorVirtualProvider? createManiaProviderOrNull()
        {
            // 仅针对 mania + EzPro 的简化实现：用反射创建 provider，避免引入 registry/额外文件。
            const string type_name = "osu.Game.Rulesets.Mania.Skinning.Editor.ManiaEzProSkinEditorVirtualProvider, osu.Game.Rulesets.Mania";

            try
            {
                var providerType = Type.GetType(type_name, throwOnError: false);
                if (providerType == null)
                    return null;

                return Activator.CreateInstance(providerType) as ISkinEditorVirtualProvider;
            }
            catch
            {
                return null;
            }
        }

        private void InitializeLeftPlayback()
        {
            var currentSkin = skinManager.CurrentSkin.Value as EzStyleProSkin ?? new EzStyleProSkin(skinManager);
            var currentBeatmap = beatmap.Value.Beatmap;

            if (provider != null)
            {
                var virtualPlayfield = provider.CreateVirtualPlayfield(currentSkin, currentBeatmap);

                leftPlaybackContainer!.Children = new Drawable[]
                {
                    virtualPlayfield.With(p =>
                    {
                        p.RelativeSizeAxes = Axes.Both;
                        p.Anchor = Anchor.Centre;
                        p.Origin = Anchor.Centre;
                    })
                };
            }
            else
            {
                // Fallback if not supported
                leftPlaybackContainer!.Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Virtual playfield not supported for this ruleset",
                        Colour = Color4.White,
                    }
                };
            }
        }

        private void InitializeCenterDisplay()
        {
            if (provider != null)
            {
                centerNoteDisplayContainer!.Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10),
                        Children = new[]
                        {
                            provider.CreateCurrentSkinNoteDisplay(skinManager.CurrentSkin.Value as EzStyleProSkin ?? new EzStyleProSkin(skinManager)),
                            provider.CreateEditedNoteDisplay(skinManager.CurrentSkin.Value as EzStyleProSkin ?? new EzStyleProSkin(skinManager)),
                        }
                    }
                };
            }
            else
            {
                centerNoteDisplayContainer!.Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Note display not supported for this ruleset",
                        Colour = Color4.White,
                    }
                };
            }
        }

        private void InitializeRightSettings()
        {
            // TODO: 添加皮肤参数控件和应用逻辑
            settingsScrollContainer!.Child = new FillFlowContainer
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
                        Text = "皮肤参数调整",
                        Colour = Color4.White,
                        Font = OsuFont.Default.With(size: 18),
                    },
                    new OsuSpriteText
                    {
                        Text = "TODO: 添加参数控件",
                        Colour = Color4.Gray,
                        Font = OsuFont.Default.With(size: 14),
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
            // 里程碑A阶段：dialog overlay 可能在当前依赖树里不可用，务必降级为直接退出。
            if (dialogOverlay == null)
            {
                Hide();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog("应用更改到皮肤？", () =>
            {
                ApplySettings();
                Hide();
            }, () => Hide()));
        }

        public void PresentGameplay()
        {
            // 作为 overlay，这里不应 Push/Present gameplay。
        }

        protected override void Update()
        {
            base.Update();

            // Overlay 不需要更新屏幕大小
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
