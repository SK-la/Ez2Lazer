// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Right settings sidebar styled like replay/player settings overlays and pause-menu settings groups.
    /// </summary>
    public partial class EzSkinEditorSidebar : ExpandingContainer
    {
        private const float padding = 10;

        /// <summary>
        /// Reserved height for the skin.ini save footer (40px button + 10px vertical padding each side).
        /// </summary>
        public const float FOOTER_HEIGHT = 60;

        public const float EXPANDED_WIDTH = SettingsToolboxGroup.CONTAINER_WIDTH + padding * 2;
        public const float CONTRACTED_WIDTH = 0;

        private Container footerContainer = null!;
        private IconButton expandButton = null!;
        private InputManager inputManager = null!;
        private FillFlowContainer groupsFlow = null!;

        public BindableBool Pinned { get; } = new BindableBool(true);

        public BindableBool ExpandedState => Expanded;

        internal float FooterReservedHeight => footerContainer.Height;

        internal float ContentBottomPadding => FillFlow.Padding.Bottom;

        protected override bool ExpandOnHover => false;

        public EzSkinEditorSidebar()
            : base(CONTRACTED_WIDTH, EXPANDED_WIDTH)
        {
            RelativeSizeAxes = Axes.Y;
            Expanded.Value = true;

            FillFlow.Spacing = new Vector2(0, 20);
            FillFlow.Padding = new MarginPadding(padding);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0), Color4.Black.Opacity(0.8f)),
                    Depth = float.MaxValue,
                    RelativeSizeAxes = Axes.Both,
                },
                expandButton = new IconButton
                {
                    Icon = FontAwesome.Solid.Cog,
                    Origin = Anchor.TopRight,
                    Anchor = Anchor.TopLeft,
                    Margin = new MarginPadding(5),
                    Action = () => Expanded.Toggle(),
                },
                footerContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Padding = new MarginPadding(padding),
                },
            });

            FillFlow.Add(new SettingsCheckbox
            {
                LabelText = EzEditorStrings.SIDEBAR_PIN_LABEL,
                Current = Pinned,
            });

            FillFlow.Add(groupsFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 20),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            Pinned.BindValueChanged(e =>
            {
                if (e.NewValue)
                    Expanded.Value = true;
            }, true);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            checkExpanded();
            return base.OnMouseMove(e);
        }

        protected override void Update()
        {
            base.Update();

            if (Expanded.Value || Pinned.Value)
                checkExpanded();
        }

        public void ApplyStrategy(IEzSkinEditorSceneStrategy strategy, EzSkinEditorSceneContext context)
        {
            groupsFlow.Clear();

            foreach (var group in strategy.CreateSidebarGroups(context))
                groupsFlow.Add(new EzSkinEditorSettingsGroup(group));

            footerContainer.Clear();
            var footer = strategy.CreateSidebarFooter(context);

            if (footer != null)
            {
                footerContainer.Height = FOOTER_HEIGHT;
                footerContainer.Child = footer;
                FillFlow.Padding = new MarginPadding
                {
                    Left = padding,
                    Right = padding,
                    Top = padding,
                    Bottom = FOOTER_HEIGHT,
                };
            }
            else
            {
                footerContainer.Height = 0;
                FillFlow.Padding = new MarginPadding(padding);
            }
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            // handle un-expanding manually because group children block hover propagation.
        }

        private void checkExpanded()
        {
            if (Pinned.Value)
            {
                Expanded.Value = true;
                return;
            }

            float screenMouseX = inputManager.CurrentState.Mouse.Position.X;

            Expanded.Value = screenMouseX >= expandButton.ScreenSpaceDrawQuad.TopLeft.X && screenMouseX <= ToScreenSpace(new Vector2(DrawWidth + EXPANDED_WIDTH, 0)).X
                             || inputManager.DraggedDrawable != null;
        }
    }
}
