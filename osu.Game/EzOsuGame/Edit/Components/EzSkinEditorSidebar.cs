// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorSidebar : Container
    {
        public const float EXPANDED_WIDTH = 250;
        public const float CONTRACTED_WIDTH = 48;

        private readonly BindableBool pinned = new BindableBool(true);

        private FillFlowContainer groupsFlow = null!;
        private Container footerContainer = null!;
        private Container sidebarContent = null!;
        private InputManager inputManager = null!;

        public IBindable<bool> Pinned => pinned;

        public BindableBool ExpandedState { get; } = new BindableBool(true);

        public EzSkinEditorSidebar()
        {
            RelativeSizeAxes = Axes.Y;
            Width = EXPANDED_WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                sidebarContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 70, Bottom = 60 },
                            Child = groupsFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(6),
                                Padding = new MarginPadding(5),
                            },
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(5),
                            Spacing = new Vector2(4),
                            Children = new Drawable[]
                            {
                                new SidebarIconButton
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
                            Padding = new MarginPadding(5),
                        },
                    },
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
            groupsFlow.Clear();
            footerContainer.Clear();

            foreach (var group in strategy.CreateSidebarGroups(context))
                groupsFlow.Add(new EzSkinEditorCollapsibleSection(group));

            var footer = strategy.CreateSidebarFooter(context);

            if (footer != null)
                footerContainer.Child = footer;
        }

        private void applyExpandedState(bool isExpanded)
        {
            Width = isExpanded ? EXPANDED_WIDTH : CONTRACTED_WIDTH;
            sidebarContent.Alpha = isExpanded ? 1 : 0;
        }

        private partial class SidebarIconButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundColour = colours.Blue3;
                Content.CornerRadius = 4;
                Height = 28;
            }
        }
    }
}
