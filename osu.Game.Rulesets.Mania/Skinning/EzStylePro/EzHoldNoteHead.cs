// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHead : CompositeDrawable
    {
        private Bindable<bool> enabledColor = null!;
        private Bindable<double> nonSquareNoteHeight = null!;

        private TextureAnimation animation = null!;
        private Container container = null!;
        private EzNoteSideLine? noteSeparatorsL;
        private EzNoteSideLine? noteSeparatorsR;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;

            nonSquareNoteHeight = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            OnSkinChanged();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            nonSquareNoteHeight.BindValueChanged(_ => updateSizes(), true);
            ezSkinConfig.OnSettingsChanged += OnConfigChanged;
            factory.OnNoteChanged += OnSkinChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                ezSkinConfig.OnSettingsChanged -= OnConfigChanged;
                factory.OnNoteChanged -= OnSkinChanged;
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

                if (EzColumnTypeManager.GetColumnType(stageDefinition.Columns, column.Index) == "S1")
                    return "green";

                int logicalIndex = 0;

                for (int i = 0; i < column.Index; i++)
                {
                    if (EzColumnTypeManager.GetColumnType(stageDefinition.Columns, i) != "S1")
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

            noteSeparatorsL = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
            };
            noteSeparatorsR = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                // Rotation = 180,
            };
            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Stretch,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    noteSeparatorsL,
                    noteSeparatorsR
                }
            });

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = factory.CreateAnimation($"{ColorPrefix}note");
                var x = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Child = animation,
                };
                if (x.Child is TextureAnimation)
                    container.Child = x;

                OnConfigChanged();
                AddInternal(container);
            }
            else
            {
                OnConfigChanged();
                AddInternal(animation);
            }

            OnConfigChanged();
        }

        private void updateSizes()
        {
            bool isSquare = factory.IsSquareNote("whitenote");
            float noteHeight = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            Height = noteHeight;

            if (container.Children.Count > 0 && container.Child is Container c)
            {
                container.Height = noteHeight / 2;
                c.Height = noteHeight;
            }
        }

        private void OnConfigChanged()
        {
            var noteColor = Color4.White;
            if (enabledColor.Value)
                noteColor = NoteColor;

            animation.Colour = noteColor;
            container.Colour = noteColor;
            noteSeparatorsL?.UpdateGlowEffect(noteColor);
            noteSeparatorsR?.UpdateGlowEffect(noteColor);

            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        private void OnSkinChanged() => loadAnimation();
    }
}
