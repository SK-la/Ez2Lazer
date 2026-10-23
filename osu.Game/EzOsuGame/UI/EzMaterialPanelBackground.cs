// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Material;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// 穿透材质面板：材质（毛玻璃 / 液态玻璃）由 <see cref="EzMaterialContainer"/> 决定，其上叠一层染色。
    /// 可见性由宿主通过 <see cref="EzMaterialOverlayAlpha.BindExclusive"/> 与 <see cref="MaterialCaptureVisible"/> 控制。
    /// </summary>
    public partial class EzMaterialPanelBackground : Container, IMaterialBackdropConsumer
    {
        /// <summary>
        /// 宿主当前是否处于需要采样下层的状态（如预览展开）。
        /// </summary>
        public bool MaterialCaptureVisible
        {
            get => materialContainer.MaterialVisible;
            set => materialContainer.MaterialVisible = value;
        }

        public bool WantsMaterialCapture => materialContainer.WantsMaterialCapture;

        /// <summary>
        /// 液态玻璃的圆角半径（屏幕像素）。未设置时按直角处理。
        /// </summary>
        public float MaterialCornerRadius
        {
            get => materialContainer.GlassCornerRadius;
            set => materialContainer.GlassCornerRadius = value;
        }

        public Box TintBox { get; private set; }

        private readonly EzMaterialContainer materialContainer;

        private Bindable<double> materialUiBlur = null!;

        /// <param name="initialTint">Veil colour laid under the material, so it is part of what the glass samples.</param>
        /// <param name="frameBufferScale">Optional downscale for the blur pass (panels use 0.5).</param>
        public EzMaterialPanelBackground(Color4 initialTint, Vector2? frameBufferScale = null)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                materialContainer = new EzMaterialContainer
                {
                    Depth = 1,
                    FrameBufferScale = frameBufferScale ?? Vector2.One,
                },
                TintBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    // 遮罩画在材质之前：压在材质之上会吃掉边缘折射与高光。模糊是线性运算，
                    // 先染色再模糊与先模糊再染色的底色基本一致，所以视觉底色不变。
                    Depth = 2,
                    Colour = initialTint,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            materialUiBlur = ezConfig.GetBindable<double>(Ez2Setting.MaterialUiBlur);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            materialUiBlur.BindValueChanged(blur => materialContainer.BlurSigma = new Vector2((float)blur.NewValue), true);
        }

        public void SyncMaterialCaptureState()
            => materialContainer.SyncMaterialCaptureState();
    }
}
