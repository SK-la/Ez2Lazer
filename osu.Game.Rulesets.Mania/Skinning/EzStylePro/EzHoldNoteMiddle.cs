// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : CompositeDrawable, IHoldNoteBody
    {
        private EzHoldNoteHittingLayer hittingLayer = null!;
        private Container noteContainer = null!;
        private Container topContainer = null!;
        private Container middleContainer = null!;
        private DrawableHoldNote? holdNoteReference;

        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig, DrawableHitObject? drawableObject)
        {
            this.ezSkinConfig = ezSkinConfig;
            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            RelativeSizeAxes = Axes.Both;
            // FillMode = FillMode.Stretch;

            // Anchor = Anchor.BottomCentre;
            // Origin = Anchor.BottomCentre;
            // Masking = true;

            hittingLayer = new EzHoldNoteHittingLayer();
            AddInternal(hittingLayer);

            if (drawableObject != null)
            {
                holdNoteReference = (DrawableHoldNote)drawableObject;
                //
                // // AccentColour.BindTo(holdNote.AccentColour);
                // hittingLayer.AccentColour.BindTo(holdNote.AccentColour);
                ((IBindable<bool>)hittingLayer.IsHolding).BindTo(holdNoteReference.IsHolding);
            }
        }

        protected override void Update()
        {
            base.Update();

            bool isSquare = factory.IsSquareNote($"{ColorPrefix}note");
            float noteHeight = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            updateContainerSizes(noteHeight);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            loadAnimation();
            factory.OnTextureNameChanged += onSkinChanged;
            ezSkinConfig.OnSettingsChanged += onSettingsChanged;
        }

        private void updateContainerSizes(float noteHeight)
        {
            topContainer.Height = noteHeight / 2;
            if (topContainer.Child is Container topInner)
                topInner.Height = noteHeight;

            middleContainer.Y = noteHeight / 2;
            middleContainer.Height = DrawHeight - noteHeight / 2;

            if (middleContainer.Child is Container middleScale)
            {
                middleScale.Scale = new Vector2(1, DrawHeight - noteHeight / 2);

                if (middleScale.Child is Container innerContainer)
                {
                    innerContainer.Height = noteHeight;
                    innerContainer.Y = -noteHeight / 2;
                }
            }
        }

        private void onSettingsChanged()
        {
            if (enabledColor.Value)
                noteContainer.Colour = NoteColor;
        }

        private void onSkinChanged()
        {
            Schedule(() =>
            {
                ClearInternal();
                loadAnimation();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            factory.OnTextureNameChanged -= onSkinChanged;
            ezSkinConfig.OnSettingsChanged -= onSettingsChanged;
        }

        protected virtual Color4 NoteColor
        {
            get
            {
                int keyMode = stageDefinition.Columns;
                int columnIndex = column.Index;
                string keyName = $"{keyMode}K_{columnIndex}";
                string fullKey = $"{EzSkinSetting.ColumnColorPrefix}:{keyName}";
                string colorStr = ezSkinConfig.Get<string>(fullKey);
                return Colour4.FromHex(colorStr);
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
            string backupComponentName = $"{ColorPrefix}note";
            var middleAnimation = factory.CreateAnimation(ComponentName);
            var tailAnimation = factory.CreateAnimation(ComponentName2);

            if (!isAnimationValid(middleAnimation))
                middleAnimation = factory.CreateAnimation(backupComponentName);

            if (!isAnimationValid(tailAnimation))
                tailAnimation = factory.CreateAnimation(backupComponentName);

            topContainer = createTopContainer(tailAnimation);
            middleContainer = createMiddleContainer(middleAnimation);

            noteContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[] { middleContainer, topContainer }
            };

            if (enabledColor.Value)
            {
                noteContainer.Colour = NoteColor;
            }

            AddInternal(noteContainer);

            Invalidate();
        }

        private bool isAnimationValid(Drawable animation)
        {
            if (animation is Container container && container.Count == 0)
                return false;

            return true;
        }

        private Container createTopContainer(Drawable animation)
        {
            return new Container
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
                    Child = animation
                }
            };
        }

        private Container createMiddleContainer(Drawable animation)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Child = animation
                    }
                }
            };
        }

        public void Recycle()
        {
            hittingLayer.Recycle();
        }
    }
}
