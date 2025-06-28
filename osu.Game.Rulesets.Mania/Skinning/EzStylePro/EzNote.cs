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
    public partial class EzNote : CompositeDrawable
    {
        private Bindable<bool> enabledColor = null!;
        private Bindable<double> nonSquareNoteHeight = null!;

        private TextureAnimation animation = null!;
        private Drawable container = null!;
        private EzNoteSideLine? noteSeparatorsL;
        private EzNoteSideLine? noteSeparatorsR;

        protected virtual bool ShowSeparators => true;
        protected virtual bool UseColorization => true;

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

            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            nonSquareNoteHeight = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            enabledColor.BindValueChanged(_ => OnConfigChanged(), true);
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

        protected virtual string ComponentName => $"{ColorPrefix}note";

        private void loadAnimation()
        {
            ClearInternal();
            animation = factory.CreateAnimation(ComponentName);
            container = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = animation
            };

            if (ShowSeparators)
            {
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
            }

            OnConfigChanged();
            AddInternal(container);
        }

        private void updateSizes()
        {
            bool isSquare = factory.IsSquareNote("whitenote");
            Height = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);
        }

        private void OnConfigChanged()
        {
            var noteColor = Color4.White;
            if (enabledColor.Value && UseColorization)
                noteColor = NoteColor;

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
