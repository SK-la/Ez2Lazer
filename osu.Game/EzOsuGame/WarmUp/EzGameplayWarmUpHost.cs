// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.WarmUp
{
    /// <summary>
    /// 离屏宿主：把一个规则集产出的预热 drawable 放进一棵会被更新、但不可见的子树里。
    /// </summary>
    /// <remarks>
    /// 很多皮肤组件（例如走 <c>Scheduler.AddOnce</c> 建纹理的 note 系列）只有真正被更新到才会触发
    /// 纹理创建，所以光 <c>LoadComponentAsync</c> 还不够——需要在加载完成后再放它更新几帧，
    /// 才能保证转圈结束后进局不会有首次解码/创建的卡顿。
    /// </remarks>
    public partial class EzGameplayWarmUpHost : CompositeDrawable, IEzGameplayWarmUp
    {
        /// <summary>加载完成后额外放行的更新帧数，用于消化 <c>Scheduler.AddOnce</c> 里挂着的纹理创建。</summary>
        private const int settle_frames = 3;

        private readonly Drawable content;
        private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int settleFramesRemaining = settle_frames;

        public EzGameplayWarmUpHost(Drawable content)
        {
            this.content = content;

            // 不可见但不能缺席：缺席（Alpha=0 且不 AlwaysPresent）会让子树被跳过更新，预热就白做了。
            Alpha = 0;
            AlwaysPresent = true;

            // 给预热子树一个正常尺度：不少皮肤组件是按 DrawWidth 决定要不要建纹理的
            // （例如 SbI 系列 `if (DrawWidth <= 1) return;`），尺寸为 0 时它们会整段跳过或空转，
            // 预热就等于没做。这里取一个接近 mania 舞台的尺寸，让组件的布局路径与进局时一致。
            Size = new Vector2(512, 768);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(content);
        }

        protected override void Update()
        {
            base.Update();

            if (completion.Task.IsCompleted)
                return;

            if (content.LoadState < LoadState.Ready)
                return;

            if (settleFramesRemaining-- > 0)
                return;

            completion.TrySetResult(true);
        }

        public Task WarmUpAsync(CancellationToken cancellationToken) => completion.Task.WaitAsync(cancellationToken);

        /// <summary>强制结束等待（加载失败等异常路径），避免门控卡到超时。</summary>
        public void ForceComplete() => completion.TrySetResult(true);
    }
}
