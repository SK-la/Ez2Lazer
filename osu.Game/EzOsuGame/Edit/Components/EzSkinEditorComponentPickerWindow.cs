// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Draggable visual component picker. Sits above the HUD layer inside the gameplay hole and hosts
    /// the Ez Pro Skin card picker (<see cref="EzEditorSectionComponentsSidebar"/>).
    /// It draws the same chrome as the sidebar's settings groups (rounded frame, bold title bar with a
    /// top-right collapse button, background that brightens on hover). Expanded it fills the gameplay
    /// hole's full height; collapsed it folds up to the title bar. Only horizontal movement is
    /// supported; it travels from the parent's left edge (the screen edge) to the sidebar's visible
    /// left edge, snapping to either end, and its position is remembered across sessions. The
    /// component-type dropdown is pinned under the header, so only the card list scrolls.
    /// </summary>
    public partial class EzSkinEditorComponentPickerWindow : CompositeDrawable
    {
        private const float width = 260;
        private const float snap_threshold = 24;

        // Same chrome metrics as the sidebar's settings groups.
        private const float header_height = 30;
        private const float corner_radius = 5;

        /// <summary>Whether the picker is currently shown.</summary>
        public BindableBool Open { get; } = new BindableBool();

        /// <summary>Whether the body is expanded. Collapsing folds the window up to the title bar.</summary>
        public BindableBool Expanded { get; } = new BindableBool(true);

        /// <summary>
        /// Width to keep clear at the right edge, in pixels. The sidebar's settings panel overlays the
        /// right part of the gameplay hole, so the window must stop at the sidebar's visible left edge
        /// rather than the parent's edge. Evaluated per frame because the panel can collapse.
        /// </summary>
        public Func<float>? RightReserve { get; set; }

        /// <summary>
        /// Remembered horizontal position as a fraction of the free travel (0 = left edge, 1 = right
        /// edge). Shared with the config so switching sessions restores the same place.
        /// </summary>
        public Bindable<float> RememberedX { get; }

        private Box background = null!;
        private InputManager inputManager = null!;

        private bool dragging;
        private bool positionRestored;

        public EzSkinEditorComponentPickerWindow(Bindable<float> rememberedX)
        {
            RememberedX = rememberedX;

            Width = width;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            // Expanded fills the gameplay hole's height (relative); collapsing switches to a fixed height.
            // Height is left at its default 0 so making the axis relative converts it to a full fill.
            RelativeSizeAxes = Axes.Y;
            Alpha = 0;
            Masking = true;
            // A hidden window must not swallow clicks meant for the HUD layer underneath.
            AlwaysPresent = true;
        }

        public override bool PropagatePositionalInputSubTree => Open.Value;

        public override bool PropagateNonPositionalInputSubTree => Open.Value;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            CornerRadius = corner_radius;

            var section = new EzEditorSectionComponentsSidebar(showComponentTypeSelector: false)
            {
                RelativeSizeAxes = Axes.X,
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.1f,
                    Colour = colourProvider.Background4,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, header_height),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = header_height,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = EzEditorStrings.PICKER_WINDOW_TITLE.ToUpper(),
                                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 17),
                                        Padding = new MarginPadding { Left = 10, Right = 30 },
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.Centre,
                                        Position = new Vector2(-15, 0),
                                        Icon = FontAwesome.Solid.Bars,
                                        Scale = new Vector2(0.75f),
                                        Action = () => Expanded.Toggle(),
                                    },
                                },
                            },
                        },
                        // Pinned selector: stays put while the card list below scrolls. SettingsItem insets
                        // its control by CONTENT_MARGINS on both sides; cancel half of that so the dropdown
                        // shares the same left margin as the scrolling card list.
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = 0, Top = 5 },
                                Child = new SettingsEnumDropdown<EzEditorComponentType>
                                {
                                    LabelText = EzSkinStrings.COMPONENT_TYPE_LABEL,
                                    Current = section.ComponentType,
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ScrollbarVisible = false,
                                Margin = new MarginPadding { Left = 0, Right = 10, Top = 5, Bottom = 10 },
                                Child = section,
                            },
                        },
                    },
                },
            };

            Open.BindValueChanged(e => this.FadeTo(e.NewValue ? 1 : 0, 200, Easing.OutQuint), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            Expanded.BindValueChanged(v => updateExpandedState(v.NewValue), true);
        }

        protected override void Update()
        {
            base.Update();

            // The parent is not sized when LoadComplete runs, so restoring there would resolve free
            // travel as 0 and stick the window to the left. Restore once the parent width is known.
            if (!positionRestored && Parent?.DrawWidth > 0)
            {
                positionRestored = true;
                X = fractionToX(Math.Clamp(RememberedX.Value, 0, 1));
            }

            // Contains() rather than IsHovered: the card list and buttons eat their own hover events, so
            // the window itself would otherwise light up only between children.
            background.FadeTo(Contains(inputManager.CurrentState.Mouse.Position) ? 1 : 0.1f, 500, Easing.OutQuint);

            // Re-clamp when the parent or the window changes size (e.g. editor window resize).
            if (!dragging)
                clampToBounds();
        }

        /// <summary>
        /// Expanded fills the gameplay hole (relative size, no arithmetic); collapsed switches to the
        /// fixed title-bar height, which clips the body away and folds the window up.
        /// </summary>
        private void updateExpandedState(bool expanded)
        {
            // Assign the height after the axis switch: the axis setter converts the old value, so the
            // explicit assignment is what decides the final fill / fixed height.
            if (expanded)
            {
                RelativeSizeAxes = Axes.Y;
                Height = 1;
            }
            else
            {
                RelativeSizeAxes = Axes.None;
                Height = header_height;
            }
        }

        /// <summary>
        /// Horizontal travel available to the window: from the parent's left edge to the sidebar's
        /// visible left edge, minus the window width.
        /// </summary>
        private float freeTravel => Math.Max(0, (Parent?.DrawWidth ?? 0) - (RightReserve?.Invoke() ?? 0) - DrawWidth);

        private float fractionToX(float fraction) => freeTravel * fraction;

        private float xToFraction(float x) => freeTravel <= 0 ? 0 : Math.Clamp(x / freeTravel, 0, 1);

        private void clampToBounds()
        {
            if (Parent == null)
                return;

            float clamped = Math.Clamp(X, 0, freeTravel);

            if (clamped != X)
                X = clamped;
        }

        private void snapToEdge()
        {
            const float left = 0;
            float right = freeTravel;

            if (Math.Abs(X - left) <= snap_threshold)
                X = left;
            else if (Math.Abs(X - right) <= snap_threshold)
                X = right;

            // Never leave the window partly outside, even when snapping was not in range.
            clampToBounds();
            RememberedX.Value = xToFraction(X);
        }

        /// <summary>
        /// Whether the given screen-space position falls inside the title bar. Only that strip is a drag
        /// handle; the body and the collapse button keep their own clicks.
        /// </summary>
        private bool overHeader(Vector2 screenSpacePos)
        {
            float localY = ToLocalSpace(screenSpacePos).Y;
            return localY >= 0 && localY <= header_height;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // The collapse button is a deeper child, so it handles its own clicks before this runs.
            return e.Button == MouseButton.Left && overHeader(e.ScreenSpaceMousePosition);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            // The framework starts a drag only for a drawable that handles the start event.
            dragging = true;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!dragging)
                return;

            // DragEvent.Delta is per-frame, so accumulate relative to the current position.
            X += e.Delta.X;
            clampToBounds();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (!dragging)
                return;

            dragging = false;
            snapToEdge();
        }
    }
}
