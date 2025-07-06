// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : CompositeDrawable, IHitExplosion
    {
        private TextureAnimation? animation;
        private TextureAnimation? animationP;
        private Container container = null!;

        private IBindable<double> noteHeightBindable = new Bindable<double>();
        private IBindable<double> columnWidthBindable = new Bindable<double>();
        private IBindable<double> specialFactorBindable = new Bindable<double>();

        // public override bool RemoveWhenNotAlive => true;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHitExplosion()
        {
            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            noteHeightBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            columnWidthBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactorBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);

            noteHeightBindable.BindValueChanged(_ => updateY(), true);
            columnWidthBindable.BindValueChanged(_ => updateY(), true);
            specialFactorBindable.BindValueChanged(_ => updateY(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            onSkinChanged();
            factory.OnNoteChanged += onSkinChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing) { factory.OnNoteChanged -= onSkinChanged; }
        }

        private void loadAnimation()
        {
            ClearInternal();

            animation = factory.CreateAnimation("noteflare");
            animationP = factory.CreateAnimation("noteflaregood");

            container = new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.None,
                Children = new Drawable[]
                    { animation },
            };

            AddInternal(container);
            updateY();
        }

        private void updateY()
        {
            bool isSpecialColumn = EzColumnTypeManager.GetColumnType(stageDefinition.Columns, column.Index) == "S1";
            double columnWidth = columnWidthBindable.Value * (isSpecialColumn ? specialFactorBindable.Value : 1);

            bool isSquare = factory.IsSquareNote("whitenote");
            float aspectRatio = factory.GetRatio("whitenote");

            float moveY = isSquare
                ? (float)columnWidth / 2 * aspectRatio
                : (float)noteHeightBindable.Value / 2 * aspectRatio;

            // baseYPosition = LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)hitPosition.Value - moveY;
            Position = new Vector2(0, -moveY);
        }

        private void onSkinChanged()
        {
            loadAnimation();
        }

        public void Animate(JudgementResult result)
        {
            loadAnimation();

            if (result.Type > HitResult.Great)
            {
                if (animationP != null) container.Add(animationP);
            }
        }
    }
}
