// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : CompositeDrawable
    {
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();
        private readonly Bindable<double> columnWidth = new Bindable<double>();
        private IBindable<double> noteHeightBindable = new Bindable<double>();
        private IBindable<double> hitPosition = new Bindable<double>();
        private IBindable<double> columnWidthBindable = new Bindable<double>();
        private IBindable<double> specialFactorBindable = new Bindable<double>();

        private TextureAnimation animation = null!;

        // [UsedImplicitly]
        private readonly Bindable<Vector2> noteSize = new Bindable<Vector2>();

        private float baseYPosition;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnWidthBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactorBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            noteHeightBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            onSkinChanged();
            factory.OnTextureNameChanged += onSkinChanged;
            noteHeightBindable.BindValueChanged(_ => updateY(), true);
            hitPosition.BindValueChanged(_ => updateY(), true);
            columnWidthBindable.BindValueChanged(_ => updateY());
            specialFactorBindable.BindValueChanged(_ => updateY());

            noteSize.BindValueChanged(_ => updateY(), true);
            IsHitting.BindValueChanged(hitting =>
            {
                ClearTransforms();
                // Logger.Log($"IsHitting changed to: {hitting.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                // animation.IsPlaying = hitting.NewValue;

                if (hitting.NewValue)
                {
                    Alpha = 1;
                    animation.Loop = true;
                    animation.GotoFrame(0);
                }
                else
                {
                    Alpha = 0;
                }
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnTextureNameChanged -= onSkinChanged;
            }
        }

        public void Recycle()
        {
            ClearTransforms();
            Alpha = 0;
        }

        private void loadAnimation()
        {
            ClearInternal();
            animation = factory.CreateAnimation("longnoteflare");

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = factory.CreateAnimation("noteflaregood");

                if (animation.FrameCount == 0)
                {
                    animation.Dispose();
                    animation = factory.CreateAnimation("noteflare");
                }
            }

            animation.Loop = true;
            AddInternal(animation);
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
            Position = new Vector2(0, baseYPosition);

            Invalidate();
        }

        private void onSkinChanged()
        {
            loadAnimation();
        }
    }
}
