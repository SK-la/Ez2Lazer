// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzFormColour : CompositeDrawable, IHasCurrentValue<Colour4>, IFormControl, IHasPopover
    {
        public Bindable<Colour4> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public LocalisableString Caption { get; init; }
        public LocalisableString HintText { get; init; }

        private readonly BindableWithCurrent<Colour4> current = new BindableWithCurrent<Colour4>();

        private FormControlBackground background = null!;
        private FormFieldCaption caption = null!;
        private Box swatch = null!;
        private OsuSpriteText hex = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public EzFormColour()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Children = new Drawable[]
                    {
                        caption = new FormFieldCaption
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Caption = Caption,
                            TooltipText = HintText,
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Width = 88,
                            Height = 28,
                            CornerRadius = 14,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                swatch = new Box { RelativeSizeAxes = Axes.Both },
                                hex = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Font = OsuFont.Default.With(size: 12),
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            current.BindValueChanged(_ =>
            {
                swatch.Colour = Current.Value;
                hex.Text = Current.Value.ToHex();
                hex.Colour = OsuColour.ForegroundTextColourFor(Current.Value);
                ValueChanged?.Invoke();
            }, true);
            current.BindDisabledChanged(_ => updateState(), true);
        }

        public Popover GetPopover() => new OsuPopover(false)
        {
            Child = new OsuColourPickerWithAlpha
            {
                Current = { BindTarget = Current },
            },
        };

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return true;
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        private void updateState()
        {
            caption.Colour = Current.Disabled ? colourProvider.Background1 : colourProvider.Content2;
            background.VisualStyle = IsDisabled ? VisualStyle.Disabled : IsHovered ? VisualStyle.Hovered : VisualStyle.Normal;
        }

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;
    }
}
