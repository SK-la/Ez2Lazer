// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNote : CompositeDrawable
    {
        public const float DEFAULT_NON_SQUARE_HEIGHT = 25f;
        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private Drawable animation = null!;
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
            enabledColor = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        protected override void Update()
        {
            base.Update();

            bool isSquare = factory.IsSquareNote(ComponentName);
            Height = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ClearInternal();
            loadAnimation();

            factory.OnTextureNameChanged += onSkinChanged;
            ezSkinConfig.OnSettingsChanged += OnConfigChanged;
        }

        private void OnConfigChanged()
        {
            if (enabledColor.Value && UseColorization)
                animation.Colour = NoteColor;
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
            ezSkinConfig.OnSettingsChanged -= OnConfigChanged;
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

        // protected virtual Colour4 NoteColorHSV =>
        //     Colour4.FromHSV(0.1f, 0.5f, 1.0f);

        private void loadAnimation()
        {
            animation = factory.CreateAnimation(ComponentName);

            if (enabledColor.Value && UseColorization)
            {
                animation.Colour = NoteColor;
                animation.Blending = new BlendingParameters
                {
                    Source = BlendingType.SrcAlpha,
                    Destination = BlendingType.One,
                };
            }

            AddInternal(animation);
        }
    }
}
