// 简单局部模糊容器，用于列背景毛玻璃效果。
// 相比 Background，不做动态分辨率降采样，仅直接动画 BlurSigma。

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class ColumnBlurLayer : BufferedContainer
    {
        public ColumnBlurLayer()
            : base(cachedFrameBuffer: true)
        {
            RelativeSizeAxes = Axes.Both;
            RedrawOnScale = false;
        }

        public void BlurTo(Vector2 sigma, double duration = 0, Easing easing = Easing.None)
        {
            if (duration <= 0)
                BlurSigma = sigma;
            else
                this.TransformTo(nameof(BlurSigma), sigma, duration, easing);
        }
    }
}
