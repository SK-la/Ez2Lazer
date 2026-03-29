// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Framework.Logging;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);
        private readonly Container sprite;

        private readonly IBindable<double> hitPositonBindable = new Bindable<double>();

        private Bindable<string> stageName = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzStageBottom()
        {
            AddLayout(layout);

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChild = sprite = new Container
            {
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            hitPositonBindable.BindTo(ezSkinInfo.HitPosition);
            stageName = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stageName.BindValueChanged(_ => OnSkinChanged(), true);
            hitPositonBindable.BindValueChanged(_ => updatePosition());

            factory.OnNoteSizeChanged += updatePosition;
        }

        private void OnSkinChanged()
        {
            sprite.Clear();

            var container = factory.CreateStage("Body");
            sprite.Add(container);

            // var judgeLine = new EzJudgementLine();
            // sprite.Add(judgeLine);
            // invalidateLayout();
            updatePosition();
        }

        private void updatePosition()
        {
            // Logger.Log($"Updating position with DrawWidth: {DrawWidth}, HitPosition: {hitPositonBindable.Value}");
            float actualPanelWidth = DrawWidth;
            float scale = actualPanelWidth / 412.0f;

            sprite.Scale = new Vector2(scale);
            sprite.Y = 205f - 384f * scale + ezSkinConfig.DefaultHitPosition - (float)hitPositonBindable.Value;

            // 计算纹理高度和位置
            // float textureHeight = sprite.Child.Height * scale;
            // float textureTopY = DrawHeight + sprite.Y - textureHeight / 2;

            // 当纹理顶部低于屏幕顶部时隐藏
            // sprite.Alpha = textureTopY != 0
            //     ? 1
            //     : 0;
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid)
            {
                updatePosition();
                layout.Validate();
            }
        }

        private void invalidateLayout() => layout.Invalidate();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            factory.OnNoteSizeChanged -= updatePosition;
        }
    }
}
