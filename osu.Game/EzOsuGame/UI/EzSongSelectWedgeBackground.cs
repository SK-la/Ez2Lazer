// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// 选歌界面面板背景：关闭时为原版 <see cref="WedgeBackground"/>；开启时在底层叠加穿透虚化。
    /// </summary>
    public partial class EzSongSelectWedgeBackground : InputBlockingContainer, IAcrylicBackdropConsumer
    {
        public float StartAlpha { get; init; } = 0.9f;

        public float FinalAlpha { get; init; } = 0.6f;

        public float WidthForGradient { get; init; } = 0.3f;

        public bool WantsAcrylicCapture => acrylicUiEnabled?.Value ?? false;

        private AcrylicBackdropDrawable acrylicBackdrop = null!;
        private WedgeBackground wedgeBackground = null!;
        private EzAcrylicCaptureController? captureController;

        private Bindable<bool> acrylicUiEnabled = null!;
        private Bindable<double> acrylicUiBlurStrength = null!;

        [Resolved(canBeNull: true)]
        private IAcrylicCaptureRegistrar? acrylicCaptureRegistrar { get; set; }

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
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
                wedgeBackground = new WedgeBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    StartAlpha = StartAlpha,
                    FinalAlpha = FinalAlpha,
                    WidthForGradient = WidthForGradient,
                },
            };

            captureController = new EzAcrylicCaptureController(acrylicCaptureRegistrar, renderer, acrylicBackdrop);
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
