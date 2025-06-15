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
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHead : CompositeDrawable
    {
        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private Bindable<double> nonSquareNoteHeight = null!;

        private TextureAnimation animation = null!;
        private Container container = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHoldNoteHead()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;

            nonSquareNoteHeight = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            nonSquareNoteHeight.ValueChanged += _ => updateSizes();
            ezSkinConfig.OnSettingsChanged += OnSettingsChanged;
            factory.OnTextureNameChanged += OnSkinChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                ezSkinConfig.OnSettingsChanged -= OnSettingsChanged;
                factory.OnTextureNameChanged -= OnSkinChanged;
            }
        }

        private void OnSkinChanged() => loadAnimation();

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

        protected virtual string ComponentName => $"{ColorPrefix}longnote/head";

        private void loadAnimation()
        {
            ClearInternal();
            animation = factory.CreateAnimation(ComponentName);

            container = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
            };

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = factory.CreateAnimation($"{ColorPrefix}note");

                container.
                    Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Child = animation,
                };

                OnSettingsChanged();
                AddInternal(container);
            }
            else
            {
                OnSettingsChanged();
                AddInternal(animation);
            }

            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        private void updateSizes()
        {
            bool isSquare = factory.IsSquareNote("whitenote");
            float noteHeight = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            Height = noteHeight;

            container.Height = noteHeight / 2;
            if (container.Child is Container containerA)
                containerA.Height = noteHeight;
        }

        private void OnSettingsChanged()
        {
            if (enabledColor.Value)
                animation.Colour = NoteColor;
        }
    }
}
