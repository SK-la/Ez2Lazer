// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Material;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 可皮肤化的圆角背景框。材质由 <see cref="EzMaterialContainer"/> 按档位切换，
    /// 通过 <see cref="IMaterialCaptureRegistrar"/> 声明捕获需求。
    /// </summary>
    public partial class EzBoxElement : CompositeDrawable, ISerialisableDrawable, IMaterialBackdropConsumer
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOX_ELEMENT_WIDTH_LABEL), nameof(EzHUDStrings.BOX_ELEMENT_WIDTH_DESCRIPTION))]
        public BindableNumber<float> BoxWidth { get; } = new BindableNumber<float>(400)
        {
            MinValue = 50,
            MaxValue = 1920,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOX_ELEMENT_HEIGHT_LABEL), nameof(EzHUDStrings.BOX_ELEMENT_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> BoxHeight { get; } = new BindableNumber<float>(80)
        {
            MinValue = 20,
            MaxValue = 1080,
            Precision = 1,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CornerRadius), nameof(SkinnableComponentStrings.CornerRadiusDescription),
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0.25f)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BACKDROP_BLUR_LABEL), nameof(EzHUDStrings.BACKDROP_BLUR_DESCRIPTION))]
        public BindableBool BlurEnabled { get; } = new BindableBool(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BLUR_STRENGTH_LABEL), nameof(EzHUDStrings.BLUR_STRENGTH_DESCRIPTION))]
        public BindableNumber<float> Blur { get; } = new BindableNumber<float>(16)
        {
            MinValue = 0,
            MaxValue = 40,
            Precision = 1,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(new Color4(1f, 1f, 1f, 0.12f));

        public bool WantsMaterialCapture => materialBackdrop.WantsMaterialCapture;

        private readonly EzMaterialContainer materialBackdrop;
        private readonly Box tintBox;

        public EzBoxElement()
        {
            Size = new Vector2(400, 80);
            Masking = true;

            InternalChildren = new Drawable[]
            {
                materialBackdrop = new EzMaterialContainer(),
                tintBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxWidth.BindValueChanged(v => Width = v.NewValue, true);
            BoxHeight.BindValueChanged(v => Height = v.NewValue, true);
            AccentColour.BindValueChanged(v => tintBox.Colour = v.NewValue, true);
            BlurEnabled.BindValueChanged(_ => SyncMaterialCaptureState(), true);
            Blur.BindValueChanged(_ => SyncMaterialCaptureState(), true);
        }

        public void SyncMaterialCaptureState()
        {
            materialBackdrop.MaterialVisible = BlurEnabled.Value && Blur.Value > 0;
            materialBackdrop.BlurSigma = new Vector2(Blur.Value);
            materialBackdrop.SyncMaterialCaptureState();
        }

        protected override void Update()
        {
            base.Update();

            float effectiveCornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
            base.CornerRadius = effectiveCornerRadius;
            materialBackdrop.GlassCornerRadius = effectiveCornerRadius;
        }
    }
}
