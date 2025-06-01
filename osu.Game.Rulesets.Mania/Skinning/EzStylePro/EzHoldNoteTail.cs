// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : CompositeDrawable
    {
        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        private EzHoldNoteHittingLayer hittingLayer = null!;
        private Container visualContainer = null!;
        private EzSkinSettingsManager? ezSkinConfig;
        private Container? noteContainer;
        private Container? topContainer;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [BackgroundDependencyLoader(true)]
        private void load(EzSkinSettingsManager ezSkinConfig, DrawableHitObject? drawableObject)
        {
            this.ezSkinConfig = ezSkinConfig;
            RelativeSizeAxes = Axes.Both;
            Alpha = 0f;

            visualContainer = new Container { RelativeSizeAxes = Axes.Both };
            AddInternal(visualContainer);

            hittingLayer = new EzHoldNoteHittingLayer { RelativeSizeAxes = Axes.Both };
            AddInternal(hittingLayer);

            if (drawableObject != null)
            {
                // accentColour.BindTo(drawableObject.AccentColour);
                // accentColour.BindValueChanged(onAccentChanged, true);

                drawableObject.HitObjectApplied += hitObjectApplied;
            }
        }

        protected override void Update()
        {
            base.Update();

            float noteHeight;

            if (IsSquare)
            {
                noteHeight = DrawWidth;
            }
            else
            {
                noteHeight = (float)(ezSkinConfig?.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value ?? EzNote.DEFAULT_NON_SQUARE_HEIGHT);
            }

            if (topContainer != null)
            {
                topContainer.Height = noteHeight / 2;

                if (topContainer.Child is Container innerContainer)
                {
                    innerContainer.Height = noteHeight;
                }
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadAnimation();
            factory.OnTextureNameChanged += onSkinChanged;
        }

        private void onSkinChanged()
        {
            Schedule(loadAnimation);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            factory.OnTextureNameChanged -= onSkinChanged;
            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;
        }

        protected virtual string ColorPrefix => "blue";
        protected virtual string ComponentSuffix => "longnote/tail";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        private void loadAnimation()
        {
            visualContainer.Clear();

            float noteHeight;

            if (IsSquare)
            {
                noteHeight = DrawWidth;
            }
            else
            {
                noteHeight = (float)(ezSkinConfig?.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value ?? EzNote.DEFAULT_NON_SQUARE_HEIGHT);
            }

            var topAnimation = factory.CreateAnimation(ComponentName);

            if (topAnimation is Container container && container.Count == 0)
            {
                string backupComponentName = $"{ColorPrefix}note";
                var animationContainer = factory.CreateAnimation(backupComponentName);

                if (animationContainer is Container containerX &&
                    containerX.Children.FirstOrDefault() is TextureAnimation animationX &&
                    animationX.FrameCount > 0)
                {
                    var texture = animationX.CurrentFrame;

                    if (texture != null)
                    {
                        float ratio = texture.Height / (float)texture.Width;
                        IsSquare = ratio >= 0.7f;
                        AspectRatio = ratio;
                    }
                }

                topContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = noteHeight / 2,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = true,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = noteHeight,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Child = factory.CreateAnimation(backupComponentName),
                    }
                };
            }
            else
            {
                topContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = noteHeight / 2,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = true,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = noteHeight,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Child = topAnimation,
                    }
                };
            }

            noteContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[] { topContainer }
            };

            visualContainer.Add(noteContainer);
        }

        protected bool IsSquare { get; private set; } = true;
        protected float AspectRatio { get; private set; } = 1.0f;

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            hittingLayer.Recycle();

            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);

            hittingLayer.IsHolding.UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHolding).BindTo(holdNoteTail.HoldNote.IsHolding);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Scale = new Vector2(1, direction.NewValue == ScrollingDirection.Up ? -1 : 1);
        }
    }
}
