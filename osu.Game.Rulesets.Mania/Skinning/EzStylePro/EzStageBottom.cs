// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Layout;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Skinning;
using osuTK;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private readonly IBindable<double> hitPositonBindable = new BindableDouble();
        private readonly IBindable<double> columnWidth = new BindableDouble();
        private readonly IBindable<double> specialFactor = new BindableDouble();
        private readonly IBindable<string> stageName = new Bindable<string>();

        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);
        private Container sprite = null!;
        private int cs;

        protected virtual bool OpenEffect => true;

        public EzStageBottom()
        {
            AddLayout(layout);
        }

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChild =
                sprite = new Container
                {
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

            cs = stageDefinition.Columns;

            hitPositonBindable.BindTo(ezSkinInfo.HitPosition);
            columnWidth.BindTo(ezSkinInfo.ColumnWidth);
            specialFactor.BindTo(ezSkinInfo.SpecialFactor);
            stageName.BindTo(ezSkinInfo.StageName);
            stageName.BindValueChanged(_ => OnSkinChanged(), true);
            hitPositonBindable.BindValueChanged(_ => invalidateLayout(), true);
            columnWidth.BindValueChanged(_ => invalidateLayout(), true);
        }

        private void OnSkinChanged()
        {
            sprite.Clear();

            var container = factory.CreateStage("Body");
            sprite.Add(container);

            // var judgeLine = new EzJudgementLine();
            // sprite.Add(judgeLine);
            invalidateLayout();
        }

        private void updateSizes()
        {
            float actualPanelWidth = DrawWidth; //ezSkinConfig.GetTotalWidth(cs);
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
                updateSizes();
                layout.Validate();
            }
        }

        private void invalidateLayout() => layout.Invalidate();
    }
}
