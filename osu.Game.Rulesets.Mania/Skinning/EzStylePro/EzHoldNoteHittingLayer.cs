// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : CompositeDrawable
    {
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();
        private TextureAnimation? animation;

        private IBindable<double> noteHeightBindable = new Bindable<double>();
        private IBindable<double> columnWidthBindable = new Bindable<double>();
        private IBindable<double> specialFactorBindable = new Bindable<double>();

        public IBindable<double> HitPosition { get; set; } = new Bindable<double>();

        // private IBindable<double> hitPosition = new Bindable<double>();
        // private float baseYPosition;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            // AutoSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            noteHeightBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            columnWidthBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactorBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            HitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            noteHeightBindable.BindValueChanged(_ => UpdateLNsLight(), true);
            columnWidthBindable.BindValueChanged(_ => UpdateLNsLight(), true);
            specialFactorBindable.BindValueChanged(_ => UpdateLNsLight(), true);
            // hitPosition.BindValueChanged(_ => updateY(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            onSkinChanged();
            factory.OnNoteChanged += onSkinChanged;
            HitPosition.BindValueChanged(pos => Y =
                LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)pos.NewValue, true);

            IsHitting.BindValueChanged(hitting =>
            {
                ClearTransforms();
                // Logger.Log($"IsHitting changed to: {hitting.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                // animation.IsPlaying = hitting.NewValue;

                if (hitting.NewValue && animation.IsNotNull() && animation.FrameCount > 0)
                {
                    Alpha = 1;
                    animation.Restart();
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
                factory.OnNoteChanged -= onSkinChanged;
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
            UpdateLNsLight();
        }

        public void UpdateLNsLight()
        {
            bool isSpecialColumn = ezSkinConfig.GetColumnType(stageDefinition.Columns, column.Index) == "S1";
            double columnWidth = columnWidthBindable.Value * (isSpecialColumn ? specialFactorBindable.Value : 1);

            bool isSquare = factory.IsSquareNote("whitenote");
            float aspectRatio = factory.GetRatio("whitenote");

            float moveY = isSquare
                ? (float)columnWidth / 2 * aspectRatio
                : (float)noteHeightBindable.Value * aspectRatio;

            Position = new Vector2(0, moveY);
        }

        private void onSkinChanged()
        {
            loadAnimation();
        }
    }
}
