// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 贴合在谱面预览面板右侧的 mod 设置侧边栏。默认只露出一条展开把手，
    /// 展开后承载已选 mod 的参数；增删 mod 仍由完整选择层负责。
    /// </summary>
    public partial class EzModSettingsDock : CompositeDrawable
    {
        /// <summary>
        /// 收起状态下露出的把手宽度。
        /// </summary>
        public const float HANDLE_WIDTH = 20;

        /// <summary>
        /// 展开状态下的侧边栏宽度。
        /// </summary>
        public const float BODY_WIDTH = 300;

        private const float handle_height = 56;
        private const float header_height = 38;
        private const float content_padding = 12;
        private const int reveal_duration = 160;

        private readonly Container body;
        private readonly Container handleContainer;
        private readonly Container handleVisual;
        private readonly ClickableContainer handleInput;
        private readonly EzMaterialPanelBackground background;
        private readonly EzMaterialPanelBackground handleBackground;
        private readonly OsuScrollContainer scroll;
        private readonly FillFlowContainer sectionsFlow;
        private readonly OsuSpriteText emptyText;

        private IBindable<IReadOnlyList<Mod>>? mods;
        private bool handleRevealed;

        /// <summary>
        /// 侧边栏是否已展开。展开时 <see cref="Drawable.Width"/> 为 <see cref="BODY_WIDTH"/>，收起时为 <see cref="HANDLE_WIDTH"/>。
        /// </summary>
        public bool IsExpanded { get; private set; }

        /// <summary>
        /// 已应用的 mod 列表。变更时重建设置区。
        /// </summary>
        public IBindable<IReadOnlyList<Mod>>? Mods
        {
            get => mods;
            set
            {
                if (ReferenceEquals(mods, value))
                    return;

                if (mods != null)
                    mods.ValueChanged -= onModsChanged;

                mods = value;

                if (mods != null)
                    mods.ValueChanged += onModsChanged;

                if (IsLoaded)
                    rebuild();
            }
        }

        /// <summary>
        /// 打开完整 mod 选择层。
        /// </summary>
        public Action? OpenModSelect { get; set; }

        public bool MaterialCaptureVisible
        {
            set
            {
                background.MaterialCaptureVisible = value;
                background.SyncMaterialCaptureState();

                handleBackground.MaterialCaptureVisible = value;
                handleBackground.SyncMaterialCaptureState();
            }
        }

        public EzModSettingsDock()
        {
            Width = HANDLE_WIDTH;

            InternalChildren = new Drawable[]
            {
                body = new Container
                {
                    Width = BODY_WIDTH,
                    RelativeSizeAxes = Axes.Y,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        background = new EzMaterialPanelBackground(Color4.Black.Opacity(0.92f)),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Top = header_height,
                                Bottom = content_padding,
                                Horizontal = content_padding
                            },
                            Child = scroll = new OsuScrollContainer(Direction.Vertical)
                            {
                                RelativeSizeAxes = Axes.Both,
                                ClampExtension = 20,
                                ScrollbarOverlapsContent = false,
                                Child = sectionsFlow = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 24),
                                }
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = header_height,
                            Children = new Drawable[]
                            {
                                new IconButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    X = 2,
                                    Scale = new Vector2(0.7f),
                                    Icon = FontAwesome.Solid.AngleDoubleLeft,
                                    IconColour = Color4.White.Opacity(0.85f),
                                    IconHoverColour = Color4.White,
                                    Action = Collapse,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    X = 30,
                                    Text = "Mod 设置",
                                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                },
                                new IconButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    X = -2,
                                    Scale = new Vector2(0.7f),
                                    Icon = FontAwesome.Solid.AngleDoubleRight,
                                    IconColour = Color4.White.Opacity(0.85f),
                                    IconHoverColour = Color4.White,
                                    Action = () => OpenModSelect?.Invoke(),
                                }
                            }
                        },
                        emptyText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Alpha = 0,
                            AlwaysPresent = true,
                            Text = "已选 mod 没有可调参数",
                            Font = OsuFont.Default.With(size: 13),
                            Colour = Color4.White.Opacity(0.6f),
                        }
                    }
                },
                // 收起时整条右侧窄带都是把手的热区，鼠标靠近才浮出可见的把手。
                handleContainer = new Container
                {
                    Width = HANDLE_WIDTH,
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Children = new Drawable[]
                    {
                        handleVisual = new Container
                        {
                            Width = HANDLE_WIDTH,
                            Height = handle_height,
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Alpha = 0,
                            Masking = true,
                            CornerRadius = EzUIStyleV1.CORNER_RADIUS_CHIP,
                            Children = new Drawable[]
                            {
                                handleBackground = new EzMaterialPanelBackground(Color4.Black.Opacity(0.92f)),
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(11),
                                    Icon = FontAwesome.Solid.AngleDoubleRight,
                                    Colour = Color4.White.Opacity(0.85f),
                                }
                            }
                        },
                        handleInput = new ClickableContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Action = Expand,
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        /// <summary>
        /// 展开侧边栏。已展开时无动作。
        /// </summary>
        public void Expand()
        {
            if (IsExpanded)
                return;

            IsExpanded = true;
            Width = BODY_WIDTH;

            setHandleRevealed(false);
            // 收起态的热区不再需要，Alpha 归零同时让整块失去焦点与输入。
            handleContainer.ClearTransforms();
            handleContainer.Alpha = 0;

            body.ClearTransforms();
            body.FadeIn(reveal_duration, Easing.OutQuint);
        }

        /// <summary>
        /// 收起侧边栏，只保留展开把手的热区。
        /// </summary>
        public void Collapse()
        {
            if (!IsExpanded)
                return;

            IsExpanded = false;

            // 立即恢复热区，鼠标本来就停在把手位置时下一帧就会浮出可见把手。
            handleContainer.ClearTransforms();
            handleContainer.Alpha = 1;

            body.ClearTransforms();
            body.FadeOut(reveal_duration, Easing.OutQuint).Finally(_ =>
            {
                if (!IsExpanded)
                    Width = HANDLE_WIDTH;
            });
        }

        protected override void Update()
        {
            base.Update();

            if (IsExpanded)
                return;

            var inputManager = GetContainingInputManager();
            setHandleRevealed(inputManager != null && handleInput.ReceivePositionalInputAt(inputManager.CurrentState.Mouse.Position));
        }

        /// <summary>
        /// 把手平时隐藏，只在鼠标靠近把手热区时浮出。
        /// </summary>
        private void setHandleRevealed(bool revealed)
        {
            if (handleRevealed == revealed)
                return;

            handleRevealed = revealed;
            handleVisual.FadeTo(revealed ? 1 : 0, revealed ? 120 : 200, Easing.OutQuint);
        }

        private void onModsChanged(ValueChangedEvent<IReadOnlyList<Mod>> _) => rebuild();

        private void rebuild()
        {
            sectionsFlow.Clear();

            int sectionCount = 0;

            if (mods != null)
            {
                foreach (var mod in mods.Value)
                {
                    var settings = mod.CreateSettingsControls().ToList();

                    if (settings.Count == 0)
                        continue;

                    sectionsFlow.Add(new ModCustomisationSection(mod, settings));
                    sectionCount++;
                }
            }

            emptyText.Alpha = sectionCount == 0 ? 1 : 0;
            scroll.Alpha = sectionCount == 0 ? 0 : 1;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => base.ReceivePositionalInputAt(screenSpacePos) && Alpha > 0;

        // 侧边栏落在预览层与选歌列表之间，空白区域也要吃掉点击与滚轮，避免穿透到后面的选歌列表。
        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;
    }
}
