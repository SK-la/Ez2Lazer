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

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : CompositeDrawable, IHitExplosion
    {
        // public override bool RemoveWhenNotAlive => true;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Bindable<double> columnWidth = new Bindable<double>();

        private readonly Bindable<double> noteHeightBindable = new Bindable<double>();
        private readonly Bindable<double> columnWidthBindable = new Bindable<double>();
        private readonly Bindable<double> specialFactorBindable = new Bindable<double>();
        private readonly Bindable<double> hitPosition = new Bindable<double>();

        private TextureAnimation animation = null!;
        private TextureAnimation animationP = null!;
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

            ezSkinConfig.BindWith(EzSkinSetting.NonSquareNoteHeight, noteHeightBindable);
            ezSkinConfig.BindWith(EzSkinSetting.ColumnWidth, columnWidthBindable);
            ezSkinConfig.BindWith(EzSkinSetting.SpecialFactor, specialFactorBindable);
            ezSkinConfig.BindWith(EzSkinSetting.HitPosition, hitPosition);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            onSkinChanged();

            noteHeightBindable.BindValueChanged(_ => updateY());
            columnWidthBindable.BindValueChanged(_ => updateY());
            specialFactorBindable.BindValueChanged(_ => updateY());
            hitPosition.BindValueChanged(_ => updateY(), true);

            factory.OnTextureNameChanged += onSkinChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnTextureNameChanged -= onSkinChanged;
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
            var tempContainer = factory.CreateAnimation("whitenote");
            float aspectRatio = 1f;

            if (tempContainer.FrameCount > 0)
            {
                aspectRatio = tempContainer.CurrentFrame.Height / (float)tempContainer.CurrentFrame.Width;
            }

            tempContainer.Dispose();

            float moveY = isSquare
                ? (float)columnWidth.Value / 2 * aspectRatio
                : (float)noteHeightBindable.Value * aspectRatio;

            baseYPosition = 110f - (float)hitPosition.Value - moveY;
            Position = new osuTK.Vector2(0, baseYPosition);

            Invalidate();
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
                container.Add(animationP);
            }
        }
    }
}
