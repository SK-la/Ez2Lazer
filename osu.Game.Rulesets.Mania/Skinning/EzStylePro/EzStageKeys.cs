// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageKeys : CompositeDrawable
    {
        private TextureAnimation animation = null!;
        private Drawable container = null!;

        protected virtual bool IsKeyPress => true;
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
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            factory.OnNoteChanged += OnSkinChanged;
            ezSkinConfig.OnSettingsChanged += OnConfigChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnNoteChanged -= OnSkinChanged;
                ezSkinConfig.OnSettingsChanged -= OnConfigChanged;
            }
        }

        private void updateSizes()
        {
            bool isSquare = factory.IsSquareNote("whitenote");
            Height = isSquare
                ? DrawWidth
                : (float)(ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value);
        }

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

        private void OnSkinChanged() => loadAnimation();

        private void OnConfigChanged()
        {
            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        protected virtual string KeyPressPrefix => IsKeyPress switch
        {
            true => "keypress/KeyBasicPress_",
            _ => "keybase/KeyBasicBase_",
        };

        protected virtual string KeyBasicSuffix
        {
            get
            {
                if (stageDefinition.EzIsSpecialColumn(column.Index))
                    return "02";

                int logicalIndex = 0;

                for (int i = 0; i < column.Index; i++)
                {
                    if (!stageDefinition.EzIsSpecialColumn(i))
                        logicalIndex++;
                }

                return logicalIndex % 2 == 0 ? "00" : "01";
            }
        }

        protected virtual string StagePrefix => EzStageBottom.StagePrefix;
        protected virtual string ComponentName => $"{StagePrefix}/{KeyPressPrefix}{KeyBasicSuffix}";
    }
}
