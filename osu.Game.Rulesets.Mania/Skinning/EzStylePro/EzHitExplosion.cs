// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : CompositeDrawable, IHitExplosion
    {
        // public override bool RemoveWhenNotAlive => true;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Bindable<double> columnWidth = new Bindable<double>();

        private IBindable<double> noteHeightBindable = new Bindable<double>();
        private IBindable<double> columnWidthBindable = new Bindable<double>();
        private IBindable<double> specialFactorBindable = new Bindable<double>();
        private IBindable<double> hitPosition = new Bindable<double>();

        private TextureAnimation? animation;
        private TextureAnimation? animationP;
        private Container container = null!;

        [UsedImplicitly]
        private float baseYPosition;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzHitExplosion()
        {
            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            noteHeightBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            columnWidthBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactorBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);

            noteHeightBindable.BindValueChanged(_ => updateY(), true);
            columnWidthBindable.BindValueChanged(_ => updateY(), true);
            specialFactorBindable.BindValueChanged(_ => updateY(), true);
            hitPosition.BindValueChanged(_ => updateY(), true);
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

            if (isDisposing)
            {
                factory.OnNoteChanged -= onSkinChanged;
            }
        }

        private void loadAnimation()
        {
            ClearInternal();

            animation = factory.CreateAnimation("noteflare");
            animationP = factory.CreateAnimation("noteflaregood");

            animation.Loop = false;
            animationP.Loop = false;

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
            bool isSpecialColumn = stageDefinition.EzIsSpecialColumn(column.Index);
            columnWidth.Value = columnWidthBindable.Value * (isSpecialColumn ? specialFactorBindable.Value : 1);

            bool isSquare = factory.IsSquareNote("whitenote");
            float aspectRatio = factory.GetRatio("whitenote");

            float moveY = isSquare
                ? (float)columnWidth.Value / 2 * aspectRatio
                : (float)noteHeightBindable.Value / 2 * aspectRatio;

            baseYPosition = 110f - (float)hitPosition.Value - moveY;
            Position = new Vector2(0, baseYPosition);
        }

        private void onSkinChanged()
        {
            loadAnimation();
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Rotation = direction.NewValue == ScrollingDirection.Up ?  90f : 0;
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
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
