// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Ez皮肤编辑界面，提供预览和参数调整功能。通过按钮打开，该场景应与GameplayScreen和SongSelect并列存在，以同样的形式进行切换。
    /// <para>分为三个区域：</para>
    /// 1.左侧, 虚拟播放场景（循环连续播放note下落，包含循环命中，和循环Miss）;
    /// 2.中间, 当前皮肤note显示 vs 编辑中note显示对比（LN的Head, Body, Tail显示容器着色边框）;
    /// 3.右侧, 设置面板, 放置相关设置，投皮面尾编辑一类。
    /// </summary>
    public partial class EzSkinEditorScreen : OsuScreen
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        // 必须这样，否则会构建失败
        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private ISkinEditorVirtualProvider? provider;

        private Container? backgroundContainer;
        private Container? leftPlaybackContainer;
        private Container? centerNoteDisplayContainer;
        private OsuScrollContainer? settingsScrollContainer;

        public EzSkinEditorScreen()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            // AddLayout(drawSizeLayout = new LayoutValue(Invalidation.DrawSize));
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            Schedule(populateSettings);
            this.FadeInFromZero(200, Easing.OutQuint);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            Schedule(populateSettings);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(200, Easing.OutQuint);
            return base.OnExiting(e);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // Background plate (use the same skin component as mania stage background).
                        backgroundContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(10),
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
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            settingsScrollContainer = new OsuScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Height = 0.9f, // 为应用按钮留出空间
                                            },
                                            new ApplySettingsButton
                                            {
                                                Text = "Apply Settings",
                                                RelativeSizeAxes = Axes.X,
                                                Height = 40,
                                                Anchor = Anchor.BottomCentre,
                                                Origin = Anchor.BottomCentre,
                                                Action = applySettings,
                                            }
                                        }
                                    },
                                }
                            }
                        }
                    }
                }
            };
        }

        public void PopulateSettings() => populateSettings();

        private void populateSettings()
        {
            // 注意：这里不要创建 HitObjectComposer（会引入编辑器依赖）。
            // 尽量确保 mania 程序集已加载，以便其在模块初始化时完成 provider 注册。

            backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
            backgroundContainer.Child.RelativeSizeAxes = Axes.Both;

            provider = createProviderOrNull(Beatmap.Value?.Beatmap);

            // 当调用时初始化屏幕
            initializeLeftPlayback();
            initializeCenterDisplay();
            initializeRightSettings();
        }

        private static ISkinEditorVirtualProvider? createProviderOrNull(IBeatmap? beatmap)
        {
            // Prefer a ruleset-registered provider when a beatmap is available.
            try
            {
                int rulesetId = beatmap?.BeatmapInfo.Ruleset.OnlineID ?? 0;

                if (rulesetId != 0)
                {
                    var fromRegistry = SkinEditorProviderRegistry.Get(rulesetId);
                    if (fromRegistry != null)
                        return fromRegistry;
                }

                // Fallback: discover any loaded type that implements ISkinEditorVirtualProvider.
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;

                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var t in types)
                    {
                        if (typeof(ISkinEditorVirtualProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            try
                            {
                                return Activator.CreateInstance(t) as ISkinEditorVirtualProvider;
                            }
                            catch
                            {
                                // ignore and continue
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static Drawable? createManiaStageBackgroundOrNull()
        {
            var lookup = tryCreateManiaSkinComponentLookupOrNull("StageBackground");
            if (lookup == null)
                return null;

            return new SkinnableDrawable(lookup)
            {
                RelativeSizeAxes = Axes.Both
            };
        }

        private static ISkinComponentLookup? tryCreateManiaSkinComponentLookupOrNull(string componentName)
        {
            const string lookup_type_name = "osu.Game.Rulesets.Mania.ManiaSkinComponentLookup, osu.Game.Rulesets.Mania";
            const string enum_type_name = "osu.Game.Rulesets.Mania.ManiaSkinComponents, osu.Game.Rulesets.Mania";

            try
            {
                var lookupType = Type.GetType(lookup_type_name, throwOnError: false);
                var enumType = Type.GetType(enum_type_name, throwOnError: false);

                if (lookupType == null || enumType == null)
                    return null;

                object componentValue = Enum.Parse(enumType, componentName, ignoreCase: false);
                return Activator.CreateInstance(lookupType, componentValue) as ISkinComponentLookup;
            }
            catch
            {
                return null;
            }
        }

        private void initializeLeftPlayback()
        {
            var currentSkin = getEditorSkin();

            if (provider != null)
            {
                var dynamicPart = provider.CreateDynamicPart(currentSkin);

                leftPlaybackContainer!.Children = new Drawable[]
                {
                    dynamicPart.With(p =>
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

        private void initializeCenterDisplay()
        {
            var currentSkin = getEditorSkin();

            if (provider != null)
            {
                centerNoteDisplayContainer!.Children = new Drawable[]
                {
                    provider.CreateStaticPart(currentSkin)
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

        private void initializeRightSettings()
        {
            var currentSkin = getEditorSkin();

            if (provider != null)
            {
                // Provider may provide a full parameters UI; prefer that when available.
                settingsScrollContainer!.Child = provider.CreateParametersPart(currentSkin);
            }
            else
            {
                // Default placeholder when provider not present
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
        }

        private void applySettings()
        {
            // TODO: 将设置应用到当前皮肤
            // 目前只是刷新中间显示
            initializeCenterDisplay();
        }

        private ISkin getEditorSkin()
        {
            var currentSkin = skinManager.CurrentSkin.Value;

            return currentSkin is EzStyleProSkin or Ez2Skin or SbISkin
                ? currentSkin
                : new EzStyleProSkin(skinManager);
        }

        private void showExitDialog()
        {
            // 里程碑A阶段：dialog overlay 可能在当前依赖树里不可用，务必降级为直接退出。
            if (dialogOverlay == null)
            {
                this.Exit();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog("应用更改到皮肤？", () =>
            {
                applySettings();
                this.Exit();
            }, this.Exit));
        }

        public void PresentGameplay()
        {
            // 作为 overlay，这里不应 Push/Present gameplay。
        }

        private static OsuSpriteText createUnavailableText(string text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };

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
