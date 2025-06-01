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
    public partial class EzHoldNoteHead : CompositeDrawable
    {
        private EzSkinSettingsManager ezSkinConfig = null!;
        private Bindable<bool> enabledColor = null!;
        private Container? noteContainer;

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

            bool isSquare = factory.IsSquareNote($"{ColorPrefix}note");
            float noteHeight = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);

            Height = noteHeight;

            if (noteContainer != null)
            {
                noteContainer.Height = noteHeight / 2;

                if (noteContainer.Child is Container containerA)
                    containerA.Height = noteHeight;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ClearInternal();
            loadAnimation();

            factory.OnTextureNameChanged += onSkinChanged;
            ezSkinConfig.OnSettingsChanged += onSettingsChanged;
        }

        private void onSettingsChanged()
        {
            if (enabledColor.Value && noteContainer != null)
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

        protected virtual string ComponentName => $"{ColorPrefix}longnote/head";

        private void loadAnimation()
        {
            var animation = factory.CreateAnimation(ComponentName);

            if (animation is Container container && container.Count == 0)
            {
                string backupComponentName = $"{ColorPrefix}note";
                var animationContainer = factory.CreateAnimation(backupComponentName);

                noteContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Masking = true,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Child = animationContainer,
                    }
                };

                if (enabledColor.Value)
                {
                    noteContainer.Colour = NoteColor;
                }

                AddInternal(noteContainer);
            }
            else
            {
                if (enabledColor.Value)
                {
                    animation.Colour = NoteColor;
                }

                AddInternal(animation);
            }
        }

        public void Recycle()
        {
            ClearTransforms();
        }
    }
}
