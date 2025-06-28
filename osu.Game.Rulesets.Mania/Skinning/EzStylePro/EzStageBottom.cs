// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private Bindable<double> hitPositon = null!;
        private Bindable<double> columnWidth = null!;
        private Drawable? sprite;

        protected virtual bool OpenEffect => true;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            hitPositon = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            hitPositon.BindValueChanged(_ => OnConfigChanged());
            columnWidth.BindValueChanged(_ => OnConfigChanged());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
            factory.OnNoteChanged += OnSkinChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                factory.OnNoteChanged -= OnSkinChanged;
            }
        }

        public static string StagePrefix => "Stage/fivekey";
        protected static string ComponentName => $"{StagePrefix}/Body";

        private void loadAnimation()
        {
            ClearInternal();

            var stageBottom = factory.CreateStage(ComponentName);
            sprite = new Container
            {
                RelativeSizeAxes = Axes.None,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                // Masking = true,
                Child = stageBottom
            };
            // sprite.Depth = float.MinValue;
            OnConfigChanged();
            AddInternal(sprite);
        }

        protected override void Update()
        {
            base.Update();
            updateSizes();
        }

        private void updateSizes()
        {
            if (sprite == null)
                return;

            float actualPanelWidth = DrawWidth;
            double scale = actualPanelWidth / 410.0;
            sprite.Scale = new Vector2((float)scale);

            Y = 274;
            Position = new Vector2(0, 660 + 110 - (float)hitPositon.Value);
        }

        private void OnConfigChanged()
        {
            Schedule(() =>
            {
                updateSizes();
                Invalidate();
            });
        }

        private void OnSkinChanged() => loadAnimation();
    }
}
