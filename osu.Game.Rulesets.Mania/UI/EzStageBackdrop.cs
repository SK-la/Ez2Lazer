// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Performance;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Stage 背景。Panel 是否显示由 <see cref="Ez2Setting.StageBackground"/> 决定，列虚化强度由 <see cref="Ez2Setting.ColumnBlur"/> 决定。
    /// Panel 画在下层。列虚化用 <see cref="AcrylicBackdropDrawable"/>，模糊自己矩形下已经画好的谱面背景和 Panel。
    /// </summary>
    public partial class EzStageBackdrop : CompositeDrawable
    {
        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);
        private readonly Container panel;
        private readonly AcrylicBackdropDrawable blur;

        private EzResourceStore resources = null!;
        private Bindable<string> backgroundName = null!;
        private Bindable<Colour4> backgroundColour = null!;
        private Bindable<double> hitPosition = null!;
        private Bindable<bool> globalHitPosition = null!;
        private Bindable<double> columnBlur = null!;
        private Bindable<bool> turboMode = null!;
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>(ScrollingDirection.Down);

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IScrollingInfo? scrollingInfo { get; set; }

        public EzStageBackdrop()
        {
            AddLayout(layout);

            RelativeSizeAxes = Axes.Both;
            BypassAutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                panel = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                blur = new AcrylicBackdropDrawable
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
            backgroundColour = ezConfig.GetBindable<Colour4>(Ez2Setting.StageBackgroundColour);
            hitPosition = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            globalHitPosition = ezConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            columnBlur = ezConfig.GetBindable<double>(Ez2Setting.ColumnBlur);
            turboMode = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode);

            if (scrollingInfo != null)
                direction.BindTo(scrollingInfo.Direction);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            backgroundName.BindValueChanged(_ => applyTexture(), true);
            backgroundColour.BindValueChanged(colour => panel.Colour = colour.NewValue, true);
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

            blur.BlurSigma = new Vector2(sigma);
            blur.EffectEnabled = sigma > 0.01f;
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

        private bool updateLayout()
        {
            if (panel.Children.Count != 1 || DrawWidth <= 0)
                return false;

            Drawable graphic = panel.Children[0];

            if (graphic.Width <= 0)
                return false;

            float hit = ManiaPlayfieldLayoutHelper.GetHitPosition(skin, globalHitPosition.Value, hitPosition.Value);

            bool receptorAtTop = direction.Value == ScrollingDirection.Up;

            graphic.Anchor = graphic.Origin = receptorAtTop ? Anchor.TopCentre : Anchor.BottomCentre;
            graphic.Y = receptorAtTop ? hit : -hit;
            graphic.Scale = new Vector2(DrawWidth / graphic.Width);
            return true;
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid && updateLayout())
                layout.Validate();
        }

        protected override void Dispose(bool isDisposing)
        {
            blur.EffectEnabled = false;

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
