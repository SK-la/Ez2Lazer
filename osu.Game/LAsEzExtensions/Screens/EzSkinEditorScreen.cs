// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
        protected override bool BlockNonPositionalInput => true;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; } = null!;

        private ISkinEditorVirtualProvider? provider;

        private Container? mainContainer;
        private Container? leftPlaybackContainer;
        private Container? centerNoteDisplayContainer;
        private Container? rightSettingsContainer;
        private OsuScrollContainer? settingsScrollContainer;
        private OsuButton? applyButton;

        public EzSkinEditorScreen()
        {
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
            // Provider 从当前 ruleset 实例获取，以保持规则集可扩展性。
            provider = ruleset.Value?.CreateInstance() as ISkinEditorVirtualProvider;

            // 当调用时初始化屏幕
            InitializeLeftPlayback();
            InitializeCenterDisplay();
            InitializeRightSettings();
        }

        private void InitializeLeftPlayback()
        {
            var currentSkin = skinSource;
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
                        p.Scale = new Vector2(0.5f); // Scale down to fit
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
                            provider.CreateCurrentSkinNoteDisplay(skinSource),
                            provider.CreateEditedNoteDisplay(skinSource),
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
