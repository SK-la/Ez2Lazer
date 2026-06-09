// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Right settings sidebar aligned with <see cref="EditorSidebar"/> layout.
    /// </summary>
    public partial class EzSkinEditorSidebar : Container
    {
        public const float EXPANDED_WIDTH = EditorSidebar.WIDTH;
        public const float CONTRACTED_WIDTH = 48;

        private readonly BindableBool pinned = new BindableBool(true);

        private EzSkinEditorSidebarBody body = null!;
        private Container footerContainer = null!;
        private InputManager inputManager = null!;

        public IBindable<bool> Pinned => pinned;

        public BindableBool ExpandedState { get; } = new BindableBool(true);

        public EzSkinEditorSidebar()
        {
            RelativeSizeAxes = Axes.Y;
            Width = EXPANDED_WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                body = new EzSkinEditorSidebarBody
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 70, Bottom = 55 },
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(EditorSidebar.PADDING),
                    Spacing = new Vector2(4),
                    Children = new Drawable[]
                    {
                        new SidebarChromeButton
                        {
                            Text = "折叠",
                            Action = () => ExpandedState.Value = !ExpandedState.Value,
                        },
                        new SettingsCheckbox
                        {
                            LabelText = "固定显示",
                            Current = pinned,
                        },
                    },
                },
                footerContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 50,
                    Padding = new MarginPadding(EditorSidebar.PADDING),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            ExpandedState.BindValueChanged(e => applyExpandedState(e.NewValue), true);
        }

        protected override void Update()
        {
            base.Update();

            if (pinned.Value || !ExpandedState.Value)
                return;

            if (!Contains(inputManager.CurrentState.Mouse.Position))
                ExpandedState.Value = false;
        }

        public void ApplyStrategy(IEzSkinEditorSceneStrategy strategy, EzSkinEditorSceneContext context)
        {
            body.ApplyStrategy(strategy, context);

            footerContainer.Clear();
            var footer = strategy.CreateSidebarFooter(context);

            if (footer != null)
                footerContainer.Child = footer;
        }

        private void applyExpandedState(bool isExpanded)
        {
            Width = isExpanded ? EXPANDED_WIDTH : CONTRACTED_WIDTH;
            Alpha = isExpanded ? 1 : 0;
        }

        private partial class EzSkinEditorSidebarBody : EditorSidebar
        {
            public void ApplyStrategy(IEzSkinEditorSceneStrategy strategy, EzSkinEditorSceneContext context)
            {
                Content.Clear();

                foreach (var group in strategy.CreateSidebarGroups(context))
                    Content.Add(new EzSkinEditorCollapsibleSection(group));
            }
        }

        private partial class SidebarChromeButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(osu.Game.Graphics.OsuColour colours)
            {
                BackgroundColour = colours.Blue3;
                Content.CornerRadius = 4;
                Height = 28;
                RelativeSizeAxes = Axes.X;
            }
        }
    }
}
