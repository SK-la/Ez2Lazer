// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// Acrylic glass panel (N): blur backdrop + tint. Visibility is owned by the host via
    /// <see cref="EzAcrylicOverlayAlpha.BindExclusive"/> (do not half-alpha classic M over this).
    /// </summary>
    public partial class EzAcrylicPanelBackground : Container, IAcrylicBackdropConsumer
    {
        /// <summary>
        /// Host panel is in a state that should sample capture (e.g. preview expanded).
        /// </summary>
        public bool AcrylicCaptureVisible { get; set; }

        public bool WantsAcrylicCapture => (acrylicUiEnabled.Value) && AcrylicCaptureVisible;

        public Box TintBox { get; private set; }

        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private EzAcrylicCaptureController? captureController;

        private Bindable<bool> acrylicUiEnabled = null!;
        private Bindable<double> acrylicUiBlurStrength = null!;

        [Resolved(canBeNull: true)]
        private IAcrylicCaptureRegistrar? acrylicCaptureRegistrar { get; set; }

        /// <param name="initialTint">Veil colour drawn over the blurred backdrop.</param>
        /// <param name="frameBufferScale">Optional downscale for the blur pass (panels use 0.5).</param>
        public EzAcrylicPanelBackground(Color4 initialTint, Vector2? frameBufferScale = null)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                acrylicBackdrop = new AcrylicBackdropDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    EffectEnabled = false,
                    FrameBufferScale = frameBufferScale ?? Vector2.One,
                },
                TintBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = initialTint,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            acrylicUiEnabled = ezConfig.GetBindable<bool>(Ez2Setting.AcrylicUiEnabled);
            acrylicUiBlurStrength = ezConfig.GetBindable<double>(Ez2Setting.AcrylicUiBlurStrength);

            captureController = new EzAcrylicCaptureController(acrylicCaptureRegistrar, acrylicBackdrop);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            acrylicUiEnabled.BindValueChanged(_ => syncAcrylicState(), true);
            acrylicUiBlurStrength.BindValueChanged(_ => syncAcrylicState(), true);
        }

        public void SyncAcrylicCaptureState()
            => syncAcrylicState();

        private void syncAcrylicState()
        {
            captureController?.Sync(WantsAcrylicCapture, (float)acrylicUiBlurStrength.Value);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                captureController?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
