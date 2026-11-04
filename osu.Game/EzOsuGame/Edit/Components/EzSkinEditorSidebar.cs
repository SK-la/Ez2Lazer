// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 右侧设置侧栏。布局只占导航轨宽度，设置面板向左叠在游戏区上；
    /// 固定显示开关只显隐设置面板，不改导航轨、不改游戏区占位。
    /// 分组选中与设置面板滚动的双向同步见 <see cref="EzSkinEditorSidebarNavigation"/>。
    /// </summary>
    public partial class EzSkinEditorSidebar : Container
    {
        private const float padding = 10;
        private const double transition_duration = ExpandingContainer.TRANSITION_DURATION;

        public const float FOOTER_HEIGHT = 60;

        public const float CONTENT_WIDTH = SettingsToolboxGroup.CONTAINER_WIDTH + padding * 2;

        /// <summary>
        /// 设置面板展开时的视觉总宽（内容 + 轨）。布局占位始终仅为轨宽。
        /// </summary>
        public static float ExpandedWidthWithRail => CONTENT_WIDTH + EzVerticalRail.WIDTH;

        private Container contentPanel = null!;
        private Container footerContainer = null!;
        private IconButton expandButton = null!;
        private InputManager inputManager = null!;
        private EzSkinEditorSidebarGroupRail rail = null!;
        private OsuScrollContainer contentScroll = null!;
        private FillFlowContainer contentFlow = null!;
        private readonly List<EzSkinEditorSettingsGroup> groupDrawables = new List<EzSkinEditorSettingsGroup>();

        private IEzSkinEditorSceneStrategy? shownStrategy;
        private Drawable? shownFooter;
        private readonly Dictionary<EzSkinEditorSettingsGroup, Action<ValueChangedEvent<bool>>> visibilityHandlers = new Dictionary<EzSkinEditorSettingsGroup, Action<ValueChangedEvent<bool>>>();

        private EzSkinEditorSidebarNavigation navigation = null!;

        // HUD 场景悬停不唤出设置面板，避免面板盖住正在拖拽组件的游玩区。
        private bool hoverExpands = true;

        public BindableBool Pinned { get; } = new BindableBool(true);

        public BindableBool ExpandedState { get; } = new BindableBool(true);

        /// <summary>
        /// Width the sidebar reserves in the workspace layout. Zero while unpinned (the panel overlays
        /// the scene and is only summoned by hover); track width while pinned-expanded, since the pin
        /// keeps the panel open. The scene re-fits to whatever space is left.
        /// </summary>
        public Bindable<float> ReservedWidth { get; } = new Bindable<float>();

        /// <summary>
        /// True while the sidebar should take layout space: pinned and showing the settings panel.
        /// Unpinned collapses and overlays so the scene keeps the full width.
        /// </summary>
        public bool ReservesSpace => Pinned.Value && ExpandedState.Value;

        /// <summary>
        /// Width taken by the reserved sidebar, or zero while it overlays.
        /// </summary>
        public float ReservedLayoutWidth => ReservesSpace ? ExpandedWidthWithRail : 0;

        internal int PageCount => navigation.Pages.Count;

        internal EzSkinEditorSettingsGroup? SelectedSection
        {
            get
            {
                int index = navigation.SelectedIndex;
                return index >= 0 && index < groupDrawables.Count ? groupDrawables[index] : null;
            }
        }

        /// <summary>
        /// 为避免与侧边栏重叠，Picker 需要距离游戏区域右边缘保留的安全距离。
        /// 该距离 = 固定 Rail 宽度 + 淡入淡出的 Settings Panel 宽度 - 已在布局中预留的侧边栏宽度。
        /// 由于预留空间已经压缩了游戏区域，再次扣除 Panel 宽度会导致双重留白。
        /// 侧边栏折叠或未预留空间时返回 0。
        /// </summary>
        public float VisiblePanelReserve =>
            Math.Max(0, EzVerticalRail.WIDTH + (CONTENT_WIDTH - padding) * contentPanel.Alpha - ReservedLayoutWidth);

        internal float FooterReservedHeight => footerContainer.Height;

        internal float ContentBottomPadding => contentFlow.Padding.Bottom;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        public EzSkinEditorSidebar()
        {
            RelativeSizeAxes = Axes.Y;
            // 占位宽度由 ReservedWidth 驱动（固定+展开时为面板+轨，其余为 0）；面板 BypassAutoSize
            // 并向左悬停，不撑大自身宽度。
            Width = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                expandButton = new IconButton
                {
                    Icon = FontAwesome.Solid.Cog,
                    Origin = Anchor.TopRight,
                    Anchor = Anchor.TopRight,
                    Margin = new MarginPadding { Right = EzVerticalRail.WIDTH + 5, Top = 5 },
                    AlwaysPresent = true,
                    Action = () => ExpandedState.Value = true,
                },
                // 设置面板：贴容器右缘并左移一个轨宽，让出导航轨位置，避免和轨叠在一起。
                contentPanel = new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Right = EzVerticalRail.WIDTH },
                    RelativeSizeAxes = Axes.Y,
                    Width = CONTENT_WIDTH,
                    BypassAutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0), Color4.Black.Opacity(0.8f)),
                            RelativeSizeAxes = Axes.Both,
                        },
                        contentScroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarVisible = false,
                            Margin = new MarginPadding { Bottom = 10 },
                            Child = contentFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 20),
                                Padding = new MarginPadding(padding),
                            },
                        },
                        footerContainer = new Container
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 0,
                            Padding = new MarginPadding(padding),
                        },
                    },
                },
                rail = new EzSkinEditorSidebarGroupRail
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
            };

            navigation = new EzSkinEditorSidebarNavigation(rail, contentScroll, groupDrawables);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;

            Pinned.BindValueChanged(e =>
            {
                if (e.NewValue)
                    ExpandedState.Value = true;

                updateReservedWidth();
            }, true);

            ExpandedState.BindValueChanged(v =>
            {
                // 只显隐设置面板；占位宽度另由 Pinned/ExpandedState 决定。
                contentPanel.FadeTo(v.NewValue ? 1 : 0, transition_duration, Easing.OutQuint);
                updateReservedWidth();
            }, true);

            contentFlow.Add(new SettingsCheckbox
            {
                LabelText = EzEditorStrings.SIDEBAR_PIN_LABEL,
                Current = Pinned,
            });
        }

        /// <summary>
        /// Pushes the space the sidebar has to occupy. Zero while overlaying, so the scene keeps the
        /// full width; panel + rail while pinned-expanded. The sidebar's own width tracks the same
        /// value so the settings panel and rail lay out from the right edge without overlapping it.
        /// </summary>
        private void updateReservedWidth()
        {
            ReservedWidth.Value = ReservedLayoutWidth;
            Width = ReservedWidth.Value;
        }

        /// <summary>
        /// 按分组下标切换（测试与外部接线）。
        /// </summary>
        internal void SelectSidebarPage(int index) => navigation.SelectPage(index);

        internal void SelectSidebarGroup(EzSkinEditorSettingsGroup group)
        {
            int index = groupDrawables.IndexOf(group);

            if (index >= 0)
                navigation.SelectPage(index);
        }

        protected override void Update()
        {
            base.Update();

            if (Pinned.Value)
            {
                ExpandedState.Value = true;
            }
            else
            {
                bool over = receiveHoverAt(inputManager.CurrentState.Mouse.Position);

                // HoverExpandsSidebar 为 false 的场景（HUD 编辑）只悬停不展开；展开态仍可由滚轮图标或
                // 固定开关进入，指针移开后收起。
                if (over && hoverExpands)
                    ExpandedState.Value = true;
                else if (!over && inputManager.DraggedDrawable == null)
                    ExpandedState.Value = false;
            }

            navigation.SyncSelectionToScroll();
        }

        private bool receiveHoverAt(Vector2 screenSpacePos)
        {
            if (rail.ReceivePositionalInputAt(screenSpacePos))
                return true;

            if (expandButton.ReceivePositionalInputAt(screenSpacePos))
                return true;

            if (ExpandedState.Value && contentPanel.ReceivePositionalInputAt(screenSpacePos))
                return true;

            return false;
        }

        /// <summary>
        /// 显示这一场景的栏目。同一场景再次进入时复用已经创建的栏目，不重建。
        /// </summary>
        public void ShowScene(IEzSkinEditorSceneStrategy strategy)
        {
            hoverExpands = strategy.HoverExpandsSidebar;

            if (ReferenceEquals(shownStrategy, strategy))
                return;

            detachSections();
            shownStrategy = strategy;

            strategy.AttachRulesets(rulesets.AvailableRulesets.Select(info => info.CreateInstance()).ToArray());

            foreach (var section in strategy.Sections)
            {
                groupDrawables.Add(section);
                var captured = section;
                Action<ValueChangedEvent<bool>> handler = e => applySectionVisible(captured, e.NewValue);
                visibilityHandlers[section] = handler;
                section.SectionVisible.BindValueChanged(handler, true);
                contentFlow.Add(section);
            }

            navigation.ShowSections(strategy.SceneType, groupDrawables);
            navigation.SuspendSync();
            Scheduler.AddOnce(navigation.ResumeSync);

            if (shownFooter != null && shownFooter.Parent == footerContainer)
                footerContainer.Remove(shownFooter, false);

            shownFooter = strategy.Footer;

            if (shownFooter != null)
            {
                footerContainer.Height = FOOTER_HEIGHT;
                footerContainer.Child = shownFooter;
                updateContentPadding();
            }
            else
                clearFooter();
        }

        private void detachSections()
        {
            foreach (var section in groupDrawables)
            {
                if (visibilityHandlers.TryGetValue(section, out var handler))
                {
                    section.SectionVisible.ValueChanged -= handler;
                    visibilityHandlers.Remove(section);
                }

                if (section.Parent == contentFlow)
                    contentFlow.Remove(section, false);
            }

            groupDrawables.Clear();
        }

        private void applySectionVisible(EzSkinEditorSettingsGroup section, bool visible)
        {
            if (visible)
                section.Show();
            else
                section.Hide();

            int index = groupDrawables.IndexOf(section);

            if (index >= 0)
                navigation.SetSectionVisible(index, visible);
        }

        private void updateContentPadding()
        {
            float bottom = footerContainer.Height > 0 ? FOOTER_HEIGHT : padding;

            contentFlow.Padding = new MarginPadding
            {
                Left = padding,
                Right = padding,
                Top = padding,
                Bottom = bottom,
            };
        }

        private void clearFooter()
        {
            footerContainer.Height = 0;
            updateContentPadding();
        }
    }
}
