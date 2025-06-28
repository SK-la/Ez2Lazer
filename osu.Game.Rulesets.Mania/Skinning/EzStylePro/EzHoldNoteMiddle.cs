// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : CompositeDrawable, IHoldNoteBody
    {
        private readonly IBindable<bool> isHitting = new Bindable<bool>();

        private DrawableHoldNote holdNote = null!;

        private TextureAnimation? middleAnimation;
        private TextureAnimation? tailAnimation;
        private Container topContainer = null!;
        private Container middleContainer = null!;
        private Container middleScaleContainer = null!;
        private Container middleInnerContainer = null!;

        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private Drawable? container;
        private Drawable? lightContainer;
        private EzHoldNoteHittingLayer hittingLayer = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHoldNoteMiddle()
        {
            RelativeSizeAxes = Axes.Both;
            // FillMode = FillMode.Stretch;

            // Anchor = Anchor.BottomCentre;
            // Origin = Anchor.BottomCentre;
            // Masking = true;
        }

        [BackgroundDependencyLoader(true)]
        private void load(EzSkinSettingsManager ezSkinConfig, DrawableHitObject drawableObject)
        {
            this.ezSkinConfig = ezSkinConfig;
            holdNote = (DrawableHoldNote)drawableObject;
            isHitting.BindTo(holdNote.IsHolding);

            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
        }

        public void Recycle()
        {
            ClearTransforms();
            hittingLayer.Recycle();
        }

        protected override void Update()
        {
            base.Update();
            updateSizes();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            factory.OnNoteChanged += OnSkinChanged;
            ezSkinConfig.OnSettingsChanged += OnSettingsChanged;
            isHitting.BindValueChanged(onIsHittingChanged, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnNoteChanged -= OnSkinChanged;
                ezSkinConfig.OnSettingsChanged -= OnSettingsChanged;
                lightContainer?.Expire();
            }
        }

        private void OnSkinChanged()
        {
            loadAnimation();
            hittingLayer = new EzHoldNoteHittingLayer
            {
                Alpha = 0,
                IsHitting = { BindTarget = isHitting }
            };
            lightContainer = new HitTargetInsetContainer
            {
                Alpha = 0,
                Child = hittingLayer
            };
        }

        private void onIsHittingChanged(ValueChangedEvent<bool> isHitting)
        {
            hittingLayer.IsHitting.Value = isHitting.NewValue;

            if (lightContainer == null)
                return;

            if (isHitting.NewValue)
            {
                lightContainer.ClearTransforms();

                if (lightContainer.Parent == null)
                    column.TopLevelContainer.Add(lightContainer);

                lightContainer.FadeIn(80);
            }
            else
            {
                lightContainer.FadeOut(120)
                              .OnComplete(d => column.TopLevelContainer.Remove(d, false));
            }
        }

        protected virtual Color4 NoteColor
        {
            get
            {
                int keyMode = stageDefinition.Columns;
                int columnIndex = column.Index;
                return ezSkinConfig.GetColumnColor(keyMode, columnIndex);
            }
        }

        protected virtual string ColorPrefix
        {
            get
            {
                if (enabledColor.Value)
                    return "white";

                if (stageDefinition.EzIsSpecialColumn(column.Index))
                    return "green";

                int logicalIndex = 0;

                for (int i = 0; i < column.Index; i++)
                {
                    if (!stageDefinition.EzIsSpecialColumn(i))
                        logicalIndex++;
                }

                return logicalIndex % 2 == 0 ? "white" : "blue";
            }
        }

        protected virtual string ComponentName => $"{ColorPrefix}longnote/middle";
        protected virtual string ComponentName2 => $"{ColorPrefix}longnote/tail";

        private void loadAnimation()
        {
            ClearInternal();
            string backupComponentName = $"{ColorPrefix}note";
            middleAnimation = factory.CreateAnimation(ComponentName);
            tailAnimation = factory.CreateAnimation(ComponentName2);

            if (middleAnimation.FrameCount == 0)
            {
                middleAnimation.Dispose();
                middleAnimation = factory.CreateAnimation(backupComponentName);
            }

            if (tailAnimation.FrameCount == 0)
            {
                tailAnimation.Dispose();
                tailAnimation = factory.CreateAnimation(backupComponentName);
            }

            topContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Child = tailAnimation
                }
            };
            middleContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = middleScaleContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = middleInnerContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Child = middleAnimation
                    }
                }
            };
            container = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[] { middleContainer, topContainer }
            };

            OnSettingsChanged();
            AddInternal(container);
        }

        private void updateSizes()
        {
            bool isSquare = factory.IsSquareNote("whitenote");
            float noteSize = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            topContainer.Height = noteSize / 2;
            if (topContainer.Child is Container topInner)
                topInner.Height = noteSize;

            float middleHeight = Math.Max(DrawHeight - noteSize / 2, noteSize / 2);

            middleContainer.Y = noteSize / 2;
            middleContainer.Height = middleHeight + 2;
            middleScaleContainer.Scale = new Vector2(1, DrawHeight - noteSize / 2);
            middleInnerContainer.Height = noteSize;
            middleInnerContainer.Y = -noteSize / 2;
            Invalidate();
        }

        private void OnSettingsChanged()
        {
            if (enabledColor.Value && container != null)
            {
                container.Colour = NoteColor;
            }

            Schedule(updateSizes);
        }
    }
}
