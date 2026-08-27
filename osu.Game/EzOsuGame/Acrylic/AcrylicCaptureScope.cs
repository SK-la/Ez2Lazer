// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;
using osuTK;
using BackgroundDrawable = osu.Game.Graphics.Backgrounds.Background;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// 惰性 Acrylic 承载层：默认直绘子树；有活跃消费者时切换为全分辨率 <see cref="BufferedContainer"/>，
    /// 供子树内的 <see cref="AcrylicBackdropDrawable"/> 采样当前离屏帧缓冲。
    /// </summary>
    /// <remarks>
    /// The capture buffer uses linear filtering (not pixel-snapped nearest). Nearest on a fullscreen
    /// wrap around song-select backgrounds makes nested blur/dim look sharp or ineffective.
    /// Pixel alignment stays on <c>AcrylicBackdropDrawable</c> effect buffers instead.
    /// </remarks>
    [Cached(typeof(IAcrylicCaptureRegistrar))]
    public partial class AcrylicCaptureScope : CompositeDrawable, IAcrylicCaptureRegistrar
    {
        private int captureRefCount;
        private int mutationGeneration;
        private readonly Drawable capturedContent;
        private BufferedContainer? activeBuffer;

        public AcrylicCaptureScope(Drawable content)
        {
            RelativeSizeAxes = Axes.Both;
            capturedContent = content;
            AddInternal(content);
        }

        public void AcquireCapture()
        {
            if (captureRefCount++ > 0)
                return;

            invokeCaptureMutation(activateCapture);
        }

        public void ReleaseCapture()
        {
            if (captureRefCount <= 0)
                return;

            if (--captureRefCount > 0)
                return;

            invokeCaptureMutation(deactivateCapture);
        }

        private void invokeCaptureMutation(Action mutation)
        {
            int generation = ++mutationGeneration;

            void runMutation()
            {
                if (generation != mutationGeneration)
                    return;

                mutation();
            }

            if (LoadState == LoadState.Loaded && ThreadSafety.IsUpdateThread)
                runMutation();
            else
                Schedule(runMutation);
        }

        private void activateCapture()
        {
            if (activeBuffer != null || captureRefCount <= 0)
                return;

            if (capturedContent.Parent == this)
                RemoveInternal(capturedContent, false);
            else if (capturedContent.Parent == activeBuffer)
                return;

            // Linear filter: nested background blur buffers must composite softly into this scope.
            AddInternal(activeBuffer = new BufferedContainer(pixelSnapping: false)
            {
                RelativeSizeAxes = Axes.Both,
                FrameBufferScale = Vector2.One,
                Child = capturedContent,
            });

            forceRedrawBlurredBuffers(capturedContent);
        }

        private void deactivateCapture()
        {
            if (activeBuffer == null || captureRefCount > 0)
                return;

            if (capturedContent.Parent == activeBuffer)
                activeBuffer.Remove(capturedContent, false);

            RemoveInternal(activeBuffer, true);
            activeBuffer = null;

            if (capturedContent.Parent != this)
                AddInternal(capturedContent);

            // Reparenting can leave cached blur framebuffers on a stale frame — refresh them.
            forceRedrawBlurredBuffers(capturedContent);
        }

        /// <summary>
        /// Invalidate nested blur buffers after capture reparenting so song-select background
        /// blur/dim is not stuck on a pre-capture cached frame.
        /// </summary>
        private static void forceRedrawBlurredBuffers(Drawable root) => visit(root);

        private static void visit(Drawable d)
        {
            switch (d)
            {
                case BackgroundDrawable background:
                {
                    Vector2 sigma = background.BlurSigma;
                    if (sigma != Vector2.Zero)
                        background.BlurTo(sigma, 0);
                    break;
                }

                case BackgroundScreenBeatmap beatmapBackground:
                    visit(beatmapBackground.CaptureSource);
                    break;

                case OsuScreenStack osuScreenStack:
                    visit(osuScreenStack.BackgroundContent);
                    break;

                case Container container:
                    foreach (var child in container.AliveChildren)
                        visit(child);
                    break;

                case ScreenStack stack:
                    if (stack.CurrentScreen is Drawable screen)
                        visit(screen);
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Dispose may run off the update thread (e.g. app shutdown). Do not reparent
                // children here — CompositeDrawable will cascade-dispose InternalChildren.
                mutationGeneration++;
                captureRefCount = 0;
                activeBuffer = null;
            }

            base.Dispose(isDisposing);
        }
    }
}
