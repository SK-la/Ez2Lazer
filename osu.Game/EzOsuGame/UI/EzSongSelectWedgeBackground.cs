// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Material;
using osu.Game.Graphics;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// Song-select wedge background: OFF = classic <see cref="WedgeBackground"/> (M);
    /// ON = blur + dark veil (N), classic wedge fully hidden.
    /// </summary>
    public partial class EzSongSelectWedgeBackground : InputBlockingContainer, IMaterialBackdropConsumer
    {
        public float StartAlpha { get; init; } = 0.9f;

        public float FinalAlpha { get; init; } = 0.6f;

        public float WidthForGradient { get; init; } = 0.3f;

        public bool WantsMaterialCapture => acrylicUiEnabled.Value;

        private AcrylicBackdropDrawable acrylicBackdrop = null!;
        private Box darkVeil = null!;
        private WedgeBackground wedgeBackground = null!;
        private EzMaterialCaptureController? captureController;

        private Bindable<bool> acrylicUiEnabled = null!;
        private Bindable<double> acrylicUiBlurStrength = null!;

        [Resolved(canBeNull: true)]
        private IMaterialCaptureRegistrar? materialCaptureRegistrar { get; set; }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, IRenderer renderer)
        {
            RelativeSizeAxes = Axes.Both;

            acrylicUiEnabled = ezConfig.GetBindable<bool>(Ez2Setting.AcrylicUiEnabled);
            acrylicUiBlurStrength = ezConfig.GetBindable<double>(Ez2Setting.AcrylicUiBlurStrength);

            InternalChildren = new Drawable[]
            {
                acrylicBackdrop = new AcrylicBackdropDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    EffectEnabled = false,
                    FrameBufferScale = Vector2.One,
                },
                darkVeil = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EzUIStyleV1.Veil,
                    Alpha = 0,
                },
                wedgeBackground = new WedgeBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    StartAlpha = StartAlpha,
                    FinalAlpha = FinalAlpha,
                    WidthForGradient = WidthForGradient,
                },
            };

            captureController = new EzMaterialCaptureController(materialCaptureRegistrar, acrylicBackdrop, renderer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // N = blur (via Sync) + darkVeil; M = classic wedgeBackground.
            EzMaterialOverlayAlpha.BindExclusive(wedgeBackground, darkVeil, acrylicUiEnabled);
            acrylicUiEnabled.BindValueChanged(_ => syncAcrylicState(), true);
            acrylicUiBlurStrength.BindValueChanged(_ => syncAcrylicState(), true);
        }

        public void SyncMaterialCaptureState()
            => syncAcrylicState();

        private void syncAcrylicState()
        {
            captureController?.Sync(acrylicUiEnabled.Value, (float)acrylicUiBlurStrength.Value);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                captureController?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
