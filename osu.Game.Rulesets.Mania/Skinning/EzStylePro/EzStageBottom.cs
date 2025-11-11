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
            hitPositon.BindValueChanged(_ => updateSizes());
            columnWidth.BindValueChanged(_ => updateSizes());
            OnSkinChanged();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            stageName.BindValueChanged(_ => OnSkinChanged());
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
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,

                Child = stageBottom
            };
            // sprite.Depth = float.MinValue;
            AddInternal(sprite); // 注释掉以隐藏stage
            Schedule(updateSizes);
        }

        private void updateSizes()
        {
            if (sprite == null)
                return;

            float actualPanelWidth = DrawWidth;
            float scale = actualPanelWidth / 410.0f;
            sprite.Scale = new Vector2(scale);

            sprite.Y = LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)hitPositon.Value - DrawHeight * 0.865f;
            // Position = new Vector2(0, 415 + 110 - (float)hitPositon.Value);
        }
    }
}
