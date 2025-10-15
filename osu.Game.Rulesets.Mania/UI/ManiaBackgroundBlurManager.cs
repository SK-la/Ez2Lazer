// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// 专门用于处理Mania模式背景虚化的组件
    /// </summary>
    public partial class ManiaBackgroundBlurManager : CompositeDrawable
    {
        // Removed ruleset reference; will be placed inside ManiaPlayfield below columns.
        private IGameplayBackgroundSource? backgroundSource;
        private Container? proxyHost;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private Bindable<double> columnBlur = new BindableDouble();
        private BufferedContainer? blurContainer;
        private Drawable? compositeProxy;

        public bool HasSource => backgroundSource != null;

        public ManiaBackgroundBlurManager()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = -10; // draw behind columns (columns default depth 0)
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 绑定ColumnBlur设置
            columnBlur = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnBlur);
            columnBlur.BindValueChanged(_ => updateBlur(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (HasSource)
                Schedule(initializeBlur);
        }

        private void initializeBlur()
        {
            if (!HasSource)
            {
                Console.WriteLine("[ManiaBlur] initializeBlur called without source");
                return;
            }

            // Prefer background+video, fallback to background only.
            compositeProxy ??= backgroundSource!.CreateBackgroundWithVideoProxy() ?? backgroundSource.CreateBackgroundOnlyProxy();

            if (compositeProxy == null)
            {
                Console.WriteLine("[ManiaBlur] No proxy content (null). Disabling blur.");
                Alpha = 0;
                return;
            }

            Console.WriteLine($"[ManiaBlur] Source acquired (video-only attempt). Proxy type={compositeProxy.GetType().Name}");
            if (blurContainer == null)
                setupBlurContainer();
            updateBlur(true);
        }

        private void setupBlurContainer()
        {
            if (blurContainer != null) return;

            Console.WriteLine("[ManiaBlur] Setting up blur container");

            blurContainer = new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                Name = "ManiaPlayfieldBlur",
                RedrawOnScale = false,
                FrameBufferScale = new osuTK.Vector2(0.5f, 0.5f),
                Children = new Drawable[]
                {
                    proxyHost = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Name = "ProxyHost",
                        Child = compositeProxy ?? new Container { Name = "ProxyPlaceholder" }
                    }
                }
            };

            AddInternal(blurContainer);
        }

        private void updateBlur(bool force = false)
        {
            if (blurContainer == null)
                return;

            if (compositeProxy == null)
            {
                if (force)
                {
                    // One retry scheduled if first force update finds no proxy.
                    Schedule(() =>
                    {
                        compositeProxy = backgroundSource?.CreateBackgroundOnlyProxy();

                        if (compositeProxy != null && proxyHost != null)
                        {
                            proxyHost.Clear();
                            proxyHost.Child = compositeProxy;
                            Console.WriteLine("[ManiaBlur] Late proxy resolved.");
                        }
                    });
                }

                blurContainer.Alpha = 0;
                return;
            }

            float raw = (float)columnBlur.Value;
            float blurAmount = raw * BackgroundScreenBeatmap.USER_BLUR_FACTOR;
            if (raw > 0 && blurAmount < 2)
                blurAmount = 2;

            bool enable = blurAmount > 0.001f;

            if (!enable)
            {
                blurContainer.BlurSigma = osuTK.Vector2.Zero;
                blurContainer.Alpha = 0;
                return;
            }

            blurContainer.Alpha = 1;
            var target = new osuTK.Vector2(blurAmount * 0.6f);
            if (force || blurContainer.BlurSigma != target)
                blurContainer.BlurSigma = target;
        }

        public void SetSource(IGameplayBackgroundSource source)
        {
            backgroundSource = source;
            Console.WriteLine("[ManiaBlur] SetSource invoked");

            if (IsLoaded)
            {
                compositeProxy = backgroundSource.CreateBackgroundWithVideoProxy() ?? backgroundSource.CreateBackgroundOnlyProxy();
                if (compositeProxy == null) Console.WriteLine("[ManiaBlur] SetSource produced null proxy, will retry later.");

                if (proxyHost != null)
                {
                    proxyHost.Clear();
                    if (compositeProxy != null)
                        proxyHost.Child = compositeProxy;
                }

                initializeBlur();
            }
        }

        protected override void Update()
        {
            base.Update();
            updateBlur();
        }
    }
}
