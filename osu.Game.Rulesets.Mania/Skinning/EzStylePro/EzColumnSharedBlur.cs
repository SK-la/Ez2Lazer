using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Storyboards.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 单例共享 Storyboard 模糊容器。各列仅创建 Proxy，实现一次渲染，多列裁剪显示，降低性能开销。
    /// </summary>
    internal static class EzColumnSharedBlur
    {
        private static readonly object sync = new object();
        private static BufferedContainer? shared;
        private static int refCount;
        private static Vector2 currentBlur;

        /// <summary>
        /// 获取（或创建）共享模糊容器的 Proxy。需与 Release 配对。
        /// </summary>
        public static Drawable? GetProxy(DrawableStoryboard storyboard)
        {
            lock (sync)
            {
                shared ??= new BufferedContainer(cachedFrameBuffer: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    RedrawOnScale = false,
                    Child = storyboard.CreateProxy() // 使用代理避免与主 Storyboard 层级冲突
                };

                refCount++;
                return shared.CreateProxy();
            }
        }

        /// <summary>
        /// 更新共享模糊强度。
        /// </summary>
        public static void UpdateBlur(Vector2 target, double duration = 200, Easing easing = Easing.OutQuint)
        {
            lock (sync)
            {
                if (shared == null) return;
                if (currentBlur == target) return;

                currentBlur = target;

                if (duration <= 0)
                    shared.BlurSigma = target;
                else
                    shared.TransformTo(nameof(BufferedContainer.BlurSigma), target, duration, easing);
            }
        }

        /// <summary>
        /// 引用释放。最后一个引用释放时销毁共享实例。
        /// </summary>
        public static void Release()
        {
            lock (sync)
            {
                if (shared == null) return;

                refCount--;

                if (refCount <= 0)
                {
                    shared.Expire();
                    shared = null;
                    currentBlur = Vector2.Zero;
                }
            }
        }
    }
}

