// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
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
    /// ON = material + dark veil (N), classic wedge fully hidden.
    /// </summary>
    public partial class EzSongSelectWedgeBackground : InputBlockingContainer, IMaterialBackdropConsumer
    {
        public float StartAlpha { get; init; } = 0.9f;

        public float FinalAlpha { get; init; } = 0.6f;

        public float WidthForGradient { get; init; } = 0.3f;

        public bool WantsMaterialCapture => materialContainer.WantsMaterialCapture;

        private EzMaterialContainer materialContainer = null!;
        private Box darkVeil = null!;
        private WedgeBackground wedgeBackground = null!;

        private Bindable<EzMaterialUiMode> materialUiMode = null!;
        private Bindable<double> materialUiBlur = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            RelativeSizeAxes = Axes.Both;

            materialUiMode = ezConfig.GetBindable<EzMaterialUiMode>(Ez2Setting.MaterialUiMode);
            materialUiBlur = ezConfig.GetBindable<double>(Ez2Setting.MaterialUiBlur);

            InternalChildren = new Drawable[]
            {
                materialContainer = new EzMaterialContainer
                {
                    Depth = 1,
                    FrameBufferScale = Vector2.One,
                },
                darkVeil = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    // 遮罩在材质之下，否则会盖掉边缘折射与高光。
                    Depth = 2,
                    Colour = EzUIStyleV1.Veil,
                    Alpha = 0,
                },
                wedgeBackground = new WedgeBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = 0,
                    StartAlpha = StartAlpha,
                    FinalAlpha = FinalAlpha,
                    WidthForGradient = WidthForGradient,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // N = material + darkVeil; M = classic wedgeBackground.
            EzMaterialOverlayAlpha.BindExclusive(wedgeBackground, darkVeil, materialUiMode);
            materialUiMode.BindValueChanged(_ => syncMaterialState(), true);
            materialUiBlur.BindValueChanged(_ => syncMaterialState(), true);
        }

        public void SyncMaterialCaptureState()
            => syncMaterialState();

        private void syncMaterialState()
        {
            materialContainer.MaterialVisible = true;
            materialContainer.BlurSigma = new Vector2((float)materialUiBlur.Value);
            materialContainer.SyncMaterialCaptureState();
        }
    }
}
