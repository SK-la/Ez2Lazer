// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Screens.Menu
{
    public partial class EzMenuLogoBackdrop : CompositeDrawable
    {
        public readonly Bindable<bool> ShowDecorations = new Bindable<bool>(true);

        private readonly Box fill;
        private readonly AcrylicBackdropDrawable acrylic;

        private EzAcrylicCaptureController? capture;

        private Bindable<EzLogoBackdropStyle> style = null!;
        private Bindable<Colour4> solidColour = null!;
        private Bindable<double> blur = null!;
        private Bindable<double> opacity = null!;

        // private Colour4 appliedLinked = EzLogoBackdropSampler.DefaultColour;

        public EzMenuLogoBackdrop()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                acrylic = new AcrylicBackdropDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = 1,
                    EffectEnabled = false,
                },
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EzLogoBackdropSampler.DefaultColour,
                },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(Ez2ConfigManager? ezConfig, IAcrylicCaptureRegistrar? registrar, EzLogoBackdropSampler? backdropSampler)
        {
            capture = new EzAcrylicCaptureController(registrar, acrylic);

            if (ezConfig == null)
                return;

            style = ezConfig.GetBindable<EzLogoBackdropStyle>(Ez2Setting.MenuLogoBackdropStyle);
            solidColour = ezConfig.GetBindable<Colour4>(Ez2Setting.MenuLogoBackdropColour);
            blur = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropBlur);
            opacity = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropOpacity);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            style.BindValueChanged(_ => apply(), true);
            solidColour.BindValueChanged(_ => apply());
            blur.BindValueChanged(_ => apply());
            opacity.BindValueChanged(_ => apply());
        }

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     if (sampler == null)
        //         return;
        //
        //     if (sampler.Colour == appliedLinked)
        //         return;
        //
        //     appliedLinked = sampler.Colour;
        //     applyLinkedFill();
        // }

        private void apply()
        {
            bool acrylicOn = style.Value == EzLogoBackdropStyle.Acrylic;

            capture?.Sync(acrylicOn, (float)blur.Value);
            ShowDecorations.Value = style.Value == EzLogoBackdropStyle.Solid;

            switch (style.Value)
            {
                case EzLogoBackdropStyle.Solid:
                    fill.Colour = solidColour.Value;
                    fill.Alpha = 1;
                    break;

                // case EzLogoBackdropStyle.Linked:
                //     applyLinkedFill();
                //     break;

                default:
                    fill.Alpha = 0;
                    break;
            }
        }

        // private void applyLinkedFill()
        // {
        //     Colour4 colour = sampler?.Colour ?? EzLogoBackdropSampler.DefaultColour;
        //     appliedLinked = colour;
        //     fill.Colour = colour;
        //     fill.Alpha = (float)opacity.Value;
        // }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                capture?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
