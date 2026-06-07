// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// 可配置穿透虚化的面板背景（Acrylic +  tint）。
    /// </summary>
    public partial class EzAcrylicPanelBackground : Container, IAcrylicBackdropConsumer
    {
        /// <summary>
        /// 宿主面板是否处于需要采样的可见状态（例如预览展开）。收起时应为 false 以释放离屏承载层引用。
        /// </summary>
        public bool AcrylicCaptureVisible { get; set; }

        public bool WantsAcrylicCapture => (acrylicUiEnabled?.Value ?? false) && AcrylicCaptureVisible;

        public Box TintBox { get; private set; }

        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private EzAcrylicCaptureController? captureController;

        private Bindable<bool> acrylicUiEnabled = null!;
        private Bindable<double> acrylicUiBlurStrength = null!;

        [Resolved(canBeNull: true)]
        private IAcrylicCaptureRegistrar? acrylicCaptureRegistrar { get; set; }

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public EzAcrylicPanelBackground(Color4 initialTint)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                acrylicBackdrop = new AcrylicBackdropDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    EffectEnabled = false,
                    FrameBufferScale = Vector2.One,
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
