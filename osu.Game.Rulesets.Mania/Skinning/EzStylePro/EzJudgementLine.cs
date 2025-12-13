// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens;
using osuTK;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzJudgementLine : CompositeDrawable
    {
        private Container sprite = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private Bindable<double> hitPositonBindable = null!;
        private Bindable<double> columnWidth = null!;
        private Bindable<string> noteSetName = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                sprite = new Container
                {
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -ezSkinConfig.DefaultHitPosition,
                }
            };

            noteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            noteSetName.BindValueChanged(_ => OnDrawableChanged(), true);

            hitPositonBindable.BindValueChanged(_ => updateSizes(), true);
            columnWidth.BindValueChanged(_ => updateSizes(), true);
        }

        protected override void Update()
        {
            base.Update();
            updateSizes();
        }

        protected void OnDrawableChanged()
        {
            sprite.Clear();

            var container = factory.CreateAnimation("JudgementLine");
            sprite.Add(container);

            // updateSizes();
        }

        private void updateSizes()
        {
            float actualPanelWidth = DrawWidth; //ezSkinConfig.GetTotalWidth(cs);
            float scale = actualPanelWidth / 412.0f;

            sprite.Scale = new Vector2(scale);
            sprite.Y = 384f + ezSkinConfig.DefaultHitPosition - (float)hitPositonBindable.Value;
        }
    }
}
