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
        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private Bindable<double> nonSquareNoteHeight = null!;

        private TextureAnimation animation = null!;
        private Drawable container = null!;
        protected virtual bool UseColorization => true;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;

            nonSquareNoteHeight = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);
            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            nonSquareNoteHeight.ValueChanged += _ => updateSizes();
            factory.OnTextureNameChanged += OnSkinChanged;
            ezSkinConfig.OnSettingsChanged += OnConfigChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnTextureNameChanged -= OnSkinChanged;
                ezSkinConfig.OnSettingsChanged -= OnConfigChanged;
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
            if (enabledColor.Value && UseColorization)
                container.Colour = NoteColor;
            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        private void OnSkinChanged() => loadAnimation();
    }
}
