// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// 惰性 Acrylic 承载层：默认直绘子树；有活跃消费者时切换为全分辨率 <see cref="BufferedContainer"/>，
    /// 供子树内的 <see cref="AcrylicBackdropDrawable"/> 采样当前离屏帧缓冲。
    /// </summary>
    public partial class AcrylicCaptureScope : CompositeDrawable, IAcrylicCaptureRegistrar
    {
        private int captureRefCount;
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
            if (LoadState == LoadState.Loaded && ThreadSafety.IsUpdateThread)
                mutation();
            else
                Schedule(mutation);
        }

        private void activateCapture()
        {
            if (activeBuffer != null || captureRefCount <= 0)
                return;

            RemoveInternal(capturedContent, false);

            AddInternal(activeBuffer = new BufferedContainer(pixelSnapping: true)
            {
                RelativeSizeAxes = Axes.Both,
                FrameBufferScale = Vector2.One,
                Child = capturedContent,
            });
        }

        private void deactivateCapture()
        {
            if (activeBuffer == null || captureRefCount > 0)
                return;

            activeBuffer.Remove(capturedContent, false);
            RemoveInternal(activeBuffer, true);
            activeBuffer = null;

            AddInternal(capturedContent);
        }
    }
}
