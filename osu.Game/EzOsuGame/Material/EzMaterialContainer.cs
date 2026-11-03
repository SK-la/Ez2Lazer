// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        private EzMaterialCaptureController? captureController;

        private Bindable<EzMaterialUiMode>? materialUiMode;

        private Bindable<double>? glassRefractionDepth;
        private Bindable<double>? glassRefractionStrength;
        private Bindable<double>? glassChromatic;
        private Bindable<double>? glassEdgeHighlight;
        private Bindable<double>? glassInnerShadow;
        private Bindable<double>? glassTintStrength;
        private Bindable<double>? glassEdgeSaturation;

        private Vector2 blurSigma = new Vector2(4);

        private float cornerRadius;

        private Color4 tint = Color4.Transparent;

        private bool materialVisible;

        private EzMaterialUiMode ownMode = EzMaterialUiMode.Default;

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
        /// 宿主自己的档位。全局档位为默认（不干预）时使用它；全局为非默认时由全局覆盖。
        /// 自带材质选项的宿主（如主菜单 logo 背板的亚克力风格）据此保持自身设置不被全局关闭牵连。
        /// </summary>
        public EzMaterialUiMode OwnMode
        {
            get => ownMode;
            set
            {
                if (ownMode == value)
                    return;

                ownMode = value;
                applyMaterialState();
            }
        }

        private EzMaterialUiMode resolvedMode
        {
            get
            {
                // 没有配置源时（例如独立测试场景）沿用 Acrylic，保持既有行为。
                if (materialUiMode == null)
                    return EzMaterialUiMode.Acrylic;

                // 全局默认表示不干预，交给宿主自己的设置。
                return materialUiMode.Value == EzMaterialUiMode.Default ? ownMode : materialUiMode.Value;
            }
        }

        /// <summary>
        /// 当前生效档位是否为非默认。宿主可据此跳过采样之外的相关开销。
        /// </summary>
        public bool MaterialEnabled => resolvedMode != EzMaterialUiMode.Default;

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
                applyGlassParameters();
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
                applyGlassParameters();
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

            // 必须直接持有 GetBindable 的弱绑定副本：再套一层 GetBoundCopy 后中间层无人强引用，
            // 被 GC 后绑定静默失效，档位切换不再通知到容器。
            materialUiMode = ezConfig?.GetBindable<EzMaterialUiMode>(Ez2Setting.MaterialUiMode);

            if (ezConfig == null)
            {
                applyMaterialState();
                return;
            }

            glassRefractionDepth = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassRefractionDepth);
            glassRefractionStrength = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassRefractionStrength);
            glassChromatic = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassChromatic);
            glassEdgeHighlight = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassEdgeHighlight);
            glassInnerShadow = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassInnerShadow);
            glassTintStrength = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassTintStrength);
            glassEdgeSaturation = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassEdgeSaturation);

            glassRefractionDepth.BindValueChanged(_ => applyGlassParameters());
            glassRefractionStrength.BindValueChanged(_ => applyGlassParameters());
            glassChromatic.BindValueChanged(_ => applyGlassParameters());
            glassEdgeHighlight.BindValueChanged(_ => applyGlassParameters());
            glassInnerShadow.BindValueChanged(_ => applyGlassParameters());
            glassTintStrength.BindValueChanged(_ => applyGlassParameters());
            glassEdgeSaturation.BindValueChanged(_ => applyGlassParameters());

            applyGlassParameters();

            materialUiMode!.BindValueChanged(_ => applyMaterialState(), true);
        }

        public void SyncMaterialCaptureState() => applyMaterialState();

        /// <summary>
        /// 把配置项与宿主形状合进玻璃参数。圆角由宿主给出，其余取自设置。
        /// </summary>
        private void applyGlassParameters()
        {
            LiquidGlassMaterial material = glass.Material;

            material.CornerRadius = cornerRadius;
            material.Tint = tint;

            if (glassRefractionDepth != null)
                material.RefractionDepth = (float)glassRefractionDepth.Value;

            if (glassRefractionStrength != null)
                material.RefractionStrength = (float)glassRefractionStrength.Value;

            if (glassChromatic != null)
                material.ChromaticStrength = (float)glassChromatic.Value;

            if (glassEdgeHighlight != null)
                material.EdgeHighlight = (float)glassEdgeHighlight.Value;

            if (glassInnerShadow != null)
                material.InnerShadow = (float)glassInnerShadow.Value;

            if (glassTintStrength != null)
                material.TintStrength = (float)glassTintStrength.Value;

            if (glassEdgeSaturation != null)
                material.EdgeSaturation = (float)glassEdgeSaturation.Value;

            glass.Material = material;
        }

        private void applyMaterialState()
        {
            EzMaterialUiMode mode = resolvedMode;
            bool wants = mode != EzMaterialUiMode.Default && MaterialVisible;

            acrylic.BlurSigma = blurSigma;
            glass.BlurSigma = blurSigma;

            captureController?.Sync(wants);

            bool draw = wants && (captureController?.CanDraw ?? true);

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
