// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public sealed partial class SettingsNote : CompositeDrawable
    {
        public readonly Bindable<Data?> Current = new Bindable<Data?>();

        // Built on first non-null value. Most settings items never show a note, so building the box and
        // text flow up-front (and then hiding them) wastes a non-trivial amount of every settings load.
        private Box? background;
        private OsuTextFlowContainer? text;

        private bool hasContent;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeDuration = 300;
            AutoSizeEasing = Easing.OutQuint;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(_ => updateDisplay(), true);
            FinishTransforms(true);
        }

        private void updateDisplay()
        {
            // Never shown and still empty: nothing to collapse, so skip the transform bookkeeping entirely.
            if (!hasContent && Current.Value == null)
                return;

            // Explicitly use ClearTransforms to clear any existing auto-size transform before modifying size / flag.
            // TODO: This is dodgy as hell and needs to go.
            ClearTransforms(false, @"baseSize");
            ClearTransforms(false, nameof(Height));

            if (Current.Value == null)
            {
                AutoSizeAxes = Axes.None;
                this.ResizeHeightTo(0, 300, Easing.OutQuint);
                this.FadeOut(250, Easing.OutQuint);
                return;
            }

            if (!hasContent)
                buildContent();

            AutoSizeAxes = Axes.Y;
            this.FadeIn(250, Easing.OutQuint);

            switch (Current.Value.Type)
            {
                case Type.Informational:
                    background!.Colour = colourProvider.Dark2;
                    text!.Colour = colourProvider.Content2;
                    break;

                case Type.Warning:
                    background!.Colour = colours.Orange1;
                    text!.Colour = colourProvider.Background5;
                    break;

                case Type.Critical:
                    background!.Colour = colours.Red1;
                    text!.Colour = colourProvider.Background5;
                    break;
            }

            text!.Text = Current.Value.Text;
        }

        private void buildContent()
        {
            hasContent = true;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Top = SettingsSection.ITEM_SPACING_V2 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 5,
                    CornerExponent = 2.5f,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            Colour = Color4.Black,
                            RelativeSizeAxes = Axes.Both,
                        },
                        text = new OsuTextFlowContainer(s => s.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold))
                        {
                            Padding = new MarginPadding(8),
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    }
                },
            };
        }

        public record Data(LocalisableString Text, Type Type);

        public enum Type
        {
            Informational,
            Warning,
            Critical,
        }
    }
}
