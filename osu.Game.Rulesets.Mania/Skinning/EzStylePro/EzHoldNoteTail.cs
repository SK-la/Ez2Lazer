// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : CompositeDrawable
    {
        private EzHoldNoteHittingLayer hittingLayer = null!;
        private TextureAnimation? animation;
        private Container container = null!;

        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private float noteSize;
        private bool isSquare;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(EzSkinSettingsManager ezSkinConfig, DrawableHitObject? drawableObject)
        {
            this.ezSkinConfig = ezSkinConfig;
            RelativeSizeAxes = Axes.Both;
            Alpha = 0f;

            hittingLayer = new EzHoldNoteHittingLayer { RelativeSizeAxes = Axes.Both };
            AddInternal(hittingLayer);

            if (drawableObject != null)
            {
                // accentColour.BindTo(drawableObject.AccentColour);
                // accentColour.BindValueChanged(onAccentChanged, true);

                drawableObject.HitObjectApplied += hitObjectApplied;
            }

            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
        }

        private void updateSizes()
        {
            isSquare = factory.IsSquareNote("whitenote");
            noteSize = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            container.Height = noteSize / 2;
            if (animation != null) animation.Height = noteSize;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadAnimation();
            factory.OnNoteChanged += onSkinChanged;
            ezSkinConfig.OnSettingsChanged += onSettingsChanged;
        }

        private void onSkinChanged()
        {
            Schedule(loadAnimation);
        }

        private void onSettingsChanged()
        {
            if (enabledColor.Value)
                container.Colour = NoteColor;
            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            factory.OnNoteChanged -= onSkinChanged;
            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;
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

        protected virtual string ColorPrefix => "blue";
        protected virtual string ComponentSuffix => "longnote/tail";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        private void loadAnimation()
        {
            ClearInternal();
            animation = factory.CreateAnimation(ComponentName);

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = factory.CreateAnimation($"{ColorPrefix}note");
            }

            container = new Container
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
                    Child = animation,
                }
            };

            onSettingsChanged();
            AddInternal(container);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);

            hittingLayer.IsHitting.UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Scale = new Vector2(1, direction.NewValue == ScrollingDirection.Up ? -1 : 1);
        }
    }
}
