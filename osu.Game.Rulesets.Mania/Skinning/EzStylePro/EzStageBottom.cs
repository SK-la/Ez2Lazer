// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private Bindable<double> hitPositonBindable = null!;
        private Bindable<double> columnWidth = null!;
        private Bindable<string> stageName = null!;
        private Drawable? sprite;
        private int cs;

        protected virtual bool OpenEffect => true;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            cs = stageDefinition.Columns;

            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            stageName.BindValueChanged(_ => OnSkinChanged(), true);
            hitPositonBindable.BindValueChanged(_ => updateSizes(), true);
            columnWidth.BindValueChanged(_ => updateSizes(), true);
            OnSkinChanged();
        }

        protected override void Update()
        {
            base.Update();
            updateSizes();
        }

        private void OnSkinChanged()
        {
            ClearInternal();

            var stageBottom = factory.CreateStage("Body");
            sprite = new Container
            {
                RelativeSizeAxes = Axes.None,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = stageBottom
            };
            sprite.Depth = -1;
            AddInternal(sprite); // 注释掉以隐藏stage
            updateSizes();
        }

        private void updateSizes()
        {
            float actualPanelWidth = DrawWidth; //ezSkinConfig.GetTotalWidth(cs);
            float scale = actualPanelWidth / 410.0f;

            if (sprite != null)
            {
                sprite.Scale = new Vector2(scale);
                sprite.Y = 220f  - 384f * scale + ezSkinConfig.DefaultHitPosition - (float)hitPositonBindable.Value;
            }

            // 计算纹理高度和位置
            // float textureHeight = sprite.Child.Height * scale;
            // float textureTopY = DrawHeight + sprite.Y - textureHeight / 2;

            // 当纹理顶部低于屏幕顶部时隐藏
            // sprite.Alpha = textureTopY != 0
            //     ? 1
            //     : 0;
        }
    }
}
