// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Game.EzOsuGame.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Material
{
    /// <summary>
    /// 穿透材质容器：按 <see cref="Ez2Setting.MaterialUiMode"/> 在毛玻璃与液态玻璃之间切换。
    /// 宿主只声明是否需要材质与模糊强度，不关心当前用的是哪种材质。
    /// </summary>
    /// <remarks>
    /// 两种材质都常驻，靠 <c>EffectEnabled</c> 互斥绘制；关闭的一侧不分配效果缓冲。
    /// 因此切换档位无需重建子树，也不会出现「宿主忘了重新打开效果」导致只剩半透明的情况。
    /// </remarks>
    public partial class EzMaterialContainer : CompositeDrawable, IMaterialBackdropConsumer
    {
        private readonly AcrylicBackdropDrawable acrylic;
        private readonly LiquidGlassDrawable glass;

        private static readonly HashSet<string> logged_diagnostics = new HashSet<string>();

        private EzMaterialCaptureController? captureController;

        private Bindable<EzMaterialUiMode>? materialUiMode;

        private Vector2 blurSigma = new Vector2(16);

        private float cornerRadius;

        private Color4 tint = Color4.Transparent;

        private bool materialVisible;

        [Resolved(canBeNull: true)]
        private IMaterialCaptureRegistrar? registrar { get; set; }

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        /// <summary>
        /// 宿主当前是否希望显示穿透材质（面板可见、卡片展开等）。默认关闭。
        /// 赋值即重算材质状态，宿主忘记另调同步也不会退回成只有半透明遮罩。
        /// </summary>
        public bool MaterialVisible
        {
            get => materialVisible;
            set
            {
                if (materialVisible == value)
                    return;

                materialVisible = value;
                applyMaterialState();
            }
        }

        /// <summary>
        /// 当前档位是否为非默认。宿主可据此跳过采样之外的相关开销。
        /// </summary>
        public bool MaterialEnabled => (materialUiMode?.Value ?? EzMaterialUiMode.Acrylic) != EzMaterialUiMode.Default;

        public bool WantsMaterialCapture => MaterialEnabled && MaterialVisible;

        /// <summary>
        /// 模糊强度（屏幕像素）。两种材质共用。
        /// </summary>
        public Vector2 BlurSigma
        {
            get => blurSigma;
            set
            {
                if (blurSigma == value)
                    return;

                blurSigma = value;
                applyMaterialState();
            }
        }

        /// <summary>
        /// 毛玻璃效果缓冲的缩放。液态玻璃需要全分辨率采样以保持边缘折射精度，不受此值影响。
        /// </summary>
        public Vector2 FrameBufferScale
        {
            get => acrylic.FrameBufferScale;
            set => acrylic.FrameBufferScale = value;
        }

        /// <summary>
        /// 液态玻璃的圆角半径（屏幕像素），用于边缘折射与高光形状。
        /// </summary>
        public float GlassCornerRadius
        {
            get => cornerRadius;
            set
            {
                if (cornerRadius == value)
                    return;

                cornerRadius = value;
                glass.Material = glass.Material with { CornerRadius = cornerRadius };
            }
        }

        /// <summary>
        /// 液态玻璃的染色。透明表示只做折射与高光，颜色交给宿主的遮罩层。
        /// </summary>
        public Color4 Tint
        {
            get => tint;
            set
            {
                if (tint == value)
                    return;

                tint = value;
                glass.Material = glass.Material with { Tint = tint };
            }
        }

        public EzMaterialContainer()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                acrylic = new AcrylicBackdropDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    EffectEnabled = false,
                    FrameBufferScale = Vector2.One,
                },
                glass = new LiquidGlassDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    EffectEnabled = false,
                    FrameBufferScale = Vector2.One,
                },
            };

            glass.Material = glass.Material with { CornerRadius = cornerRadius, Tint = tint };
        }

        [BackgroundDependencyLoader(true)]
        private void load(Ez2ConfigManager? ezConfig)
        {
            captureController = new EzMaterialCaptureController(registrar, renderer);
            materialUiMode = ezConfig?.GetBindable<EzMaterialUiMode>(Ez2Setting.MaterialUiMode).GetBoundCopy();

            if (materialUiMode != null)
                materialUiMode.BindValueChanged(_ => applyMaterialState(), true);
            else
                applyMaterialState();
        }

        public void SyncMaterialCaptureState() => applyMaterialState();

        private void applyMaterialState()
        {
            EzMaterialUiMode mode = materialUiMode?.Value ?? EzMaterialUiMode.Acrylic;
            bool wants = mode != EzMaterialUiMode.Default && MaterialVisible;

            acrylic.BlurSigma = blurSigma;
            glass.BlurSigma = blurSigma;

            captureController?.Sync(wants);

            bool draw = wants && (captureController?.CanDraw ?? true);

            string diagKey = $"{Parent?.GetType().Name}|{mode}|{MaterialVisible}|{registrar == null}|{renderer.SupportsBackbufferRegionCopy}|{captureController?.CanDraw}|{draw}|{DrawSize}";
            if (logged_diagnostics.Add(diagKey))
                Framework.Logging.Logger.Log($"[EzMaterial] {diagKey}", level: Framework.Logging.LogLevel.Important);

            acrylic.EffectEnabled = draw && mode == EzMaterialUiMode.Acrylic;
            glass.EffectEnabled = draw && mode == EzMaterialUiMode.LiquidGlass;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                captureController?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
