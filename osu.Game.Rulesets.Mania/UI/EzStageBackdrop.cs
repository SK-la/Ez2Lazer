// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Performance;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Stage 背景：Panel 图和列虚化在同一个组件里。虚化开启时图只进入这一次虚化。
    /// </summary>
    public partial class EzStageBackdrop : CompositeDrawable
    {
        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);
        private readonly PanelLayer panel;
        private readonly BackdropBlurDrawable blur;
        private StageBlurSources? captureSources;

        private EzResourceStore resources = null!;
        private Bindable<string> backgroundName = null!;
        private Bindable<double> hitPosition = null!;
        private Bindable<bool> globalHitPosition = null!;
        private Bindable<double> columnBlur = null!;
        private Bindable<bool> turboMode = null!;
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>(ScrollingDirection.Down);

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IScrollingInfo? scrollingInfo { get; set; }

        [Resolved(canBeNull: true)]
        private IBackdropCaptureSourceProvider? gameplayCapture { get; set; }

        public EzStageBackdrop()
        {
            AddLayout(layout);

            RelativeSizeAxes = Axes.Both;
            BypassAutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                panel = new PanelLayer(),
                blur = new BackdropBlurDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    EffectEnabled = false,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(EzResourceStore resources, Ez2ConfigManager ezConfig)
        {
            this.resources = resources;
            backgroundName = ezConfig.GetBindable<string>(Ez2Setting.StageBackground);
            hitPosition = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            globalHitPosition = ezConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            columnBlur = ezConfig.GetBindable<double>(Ez2Setting.ColumnBlur);
            turboMode = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode);

            if (scrollingInfo != null)
                direction.BindTo(scrollingInfo.Direction);

            captureSources = new StageBlurSources(gameplayCapture, panel);
            blur.CaptureSourceProvider = captureSources;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            backgroundName.BindValueChanged(_ => applyTexture(), true);
            hitPosition.BindValueChanged(_ => layout.Invalidate());
            globalHitPosition.BindValueChanged(_ => layout.Invalidate());
            direction.BindValueChanged(_ => layout.Invalidate());
            skin.SourceChanged += onSkinChanged;

            columnBlur.BindValueChanged(_ => updateBlur(), true);
            turboMode.BindValueChanged(_ => updateBlur());
        }

        private void onSkinChanged() => layout.Invalidate();

        private void updateBlur()
        {
            // 列模糊不走极速模式的配置压制，只在这个消费点跳过，避免换皮肤时把压制值写回皮肤 JSON。
            float sigma = EzTurboMode.Active ? 0 : (float)columnBlur.Value * 50;
            bool enabled = sigma > 0.01f;

            panel.DrawSharp = !enabled;
            blur.BlurSigma = new Vector2(sigma);
            blur.EffectEnabled = enabled;
        }

        private void applyTexture()
        {
            panel.Clear(disposeChildren: true);

            string name = backgroundName.Value;

            if (string.IsNullOrEmpty(name))
            {
                panel.Masking = false;
                panel.Hide();
                return;
            }

            Drawable? graphic = resources.GetAnimation(new EzAnimationRequest
            {
                Path = $"Panel/{name}",
                Usage = EzTextureUsage.Large,
            });

            if (graphic == null)
            {
                panel.Masking = false;
                panel.Hide();
                return;
            }

            panel.Add(graphic);
            panel.Masking = true;
            panel.Show();
            layout.Invalidate();
        }

        private void updateLayout()
        {
            if (panel.Children.Count != 1 || DrawWidth <= 0)
                return;

            Drawable graphic = panel.Children[0];

            if (graphic.Width <= 0)
                return;

            float hit = globalHitPosition.Value
                ? (float)hitPosition.Value
                : skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                      new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.HitPosition))?.Value
                  ?? (float)hitPosition.Value;

            bool receptorAtTop = direction.Value == ScrollingDirection.Up;

            graphic.Anchor = graphic.Origin = receptorAtTop ? Anchor.TopCentre : Anchor.BottomCentre;
            graphic.Y = receptorAtTop ? hit : -hit;
            graphic.Scale = new Vector2(DrawWidth / graphic.Width);
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid)
            {
                updateLayout();
                layout.Validate();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            blur.EffectEnabled = false;
            blur.CaptureSourceProvider = null;
            blur.CaptureTarget = null;
            blur.CaptureTargets.Clear();
            captureSources?.Dispose();
            captureSources = null;

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// 虚化开启时不参与父级绘制，只作为这一次虚化的捕获源。
        /// </summary>
        private sealed partial class PanelLayer : Container
        {
            public bool DrawSharp
            {
                get => drawSharp;
                set
                {
                    if (drawSharp == value)
                        return;

                    drawSharp = value;
                    Invalidate(Invalidation.Presence);
                }
            }

            private bool drawSharp = true;

            public PanelLayer()
            {
                RelativeSizeAxes = Axes.Both;
            }

            public override bool IsPresent => DrawSharp && base.IsPresent;
        }

        private sealed class StageBlurSources : IDisposable, IBackdropCaptureSourceProvider
        {
            private readonly IBackdropCaptureSourceProvider? gameplay;
            private readonly Drawable panel;
            private readonly List<Drawable> sources = new List<Drawable>();

            private event Action sourcesChanged = delegate { };

            event Action IBackdropCaptureSourceProvider.SourcesChanged
            {
                add => sourcesChanged += value;
                remove => sourcesChanged -= value;
            }

            IReadOnlyList<Drawable> IBackdropCaptureSourceProvider.CaptureSources => sources;

            public StageBlurSources(IBackdropCaptureSourceProvider? gameplay, Drawable panel)
            {
                this.gameplay = gameplay;
                this.panel = panel;

                if (gameplay != null)
                    gameplay.SourcesChanged += rebuild;

                rebuild();
            }

            private void rebuild()
            {
                sources.Clear();

                if (gameplay?.CaptureSources != null)
                {
                    foreach (Drawable source in gameplay.CaptureSources)
                        sources.Add(source);
                }

                sources.Add(panel);
                sourcesChanged.Invoke();
            }

            public void Dispose()
            {
                if (gameplay != null)
                    gameplay.SourcesChanged -= rebuild;
            }
        }
    }
}
