// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private Bindable<double> hitPositon = null!;
        private Bindable<double> columnWidth = null!;
        private Drawable? sprite;

        protected virtual bool OpenEffect => true;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private Bindable<string> stageName = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;

            hitPositon = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            OnSkinChanged();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            stageName.BindValueChanged(_ => OnSkinChanged());
            hitPositon.BindValueChanged(_ => updateSizes());
            columnWidth.BindValueChanged(_ => updateSizes());
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
            // AddInternal(sprite); // 注释掉以隐藏stage
            Schedule(updateSizes);
        }

        private void updateSizes()
        {
            if (sprite == null)
                return;

            float actualPanelWidth = DrawWidth;
            float scale = actualPanelWidth / 410.0f;
            sprite.Scale = new Vector2(scale);

            sprite.Y = (ezSkinConfig.DefaultHitPosition - (float)hitPositon.Value) + (247f * scale);
            // Position = new Vector2(0, 415 + 110 - (float)hitPositon.Value);
        }
    }
}
