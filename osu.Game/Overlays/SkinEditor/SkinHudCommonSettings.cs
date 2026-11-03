// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// 皮肤编辑器 HUD 组件的通用设置（坐标、等比缩放）。
    /// </summary>
    internal sealed class SkinHudCommonSettings
    {
        private const float position_slider_range = 2000;
        private const float scale_min = 0.01f;
        private const float scale_max = 10f;

        private readonly Drawable component;

        private readonly BindableFloat positionX = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private readonly BindableFloat positionY = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private readonly BindableFloat scale = new BindableFloat
        {
            MinValue = scale_min,
            MaxValue = scale_max,
            Precision = 0.01f,
            Default = 1,
            Value = 1,
        };

        private bool internalSliderUpdate;

        public static SkinHudCommonSettings? TryCreate(Drawable component)
        {
            if (component is not ISerialisableDrawable)
                return null;

            return new SkinHudCommonSettings(component);
        }

        private SkinHudCommonSettings(Drawable component)
        {
            this.component = component;

            positionX.Value = component.Position.X;
            positionY.Value = component.Position.Y;
            scale.Value = component.Scale.X;

            positionX.BindValueChanged(_ => updateComponentPositionFromSliders());
            positionY.BindValueChanged(_ => updateComponentPositionFromSliders());
            scale.BindValueChanged(_ => updateComponentScaleFromSlider());
        }

        public Drawable[] CreateControls() => new Drawable[]
        {
            new SettingsSlider<float>
            {
                LabelText = "Position X",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionX,
                KeyboardStep = positionX.Precision,
            },
            new SettingsSlider<float>
            {
                LabelText = "Position Y",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionY,
                KeyboardStep = positionY.Precision,
            },
            new SettingsSlider<float>
            {
                LabelText = "Scale",
                TooltipText = SkinEditorStrings.ResetScale,
                Current = scale,
                KeyboardStep = scale.Precision,
            },
        };

        public void SyncFromComponent()
        {
            if (internalSliderUpdate)
                return;

            // 同步期间必须屏蔽回写：设置其中一个滑条会经 ValueChanged 立刻写回组件，
            // 而其余滑条还是旧值，组件会被静默改回去。
            internalSliderUpdate = true;

            if (positionX.Value != component.Position.X)
                positionX.Value = component.Position.X;

            if (positionY.Value != component.Position.Y)
                positionY.Value = component.Position.Y;

            if (scale.Value != component.Scale.X)
                scale.Value = component.Scale.X;

            internalSliderUpdate = false;
        }

        private void updateComponentPositionFromSliders()
        {
            if (internalSliderUpdate)
                return;

            internalSliderUpdate = true;
            component.Position = new Vector2(positionX.Value, positionY.Value);
            internalSliderUpdate = false;
        }

        private void updateComponentScaleFromSlider()
        {
            if (internalSliderUpdate)
                return;

            internalSliderUpdate = true;
            component.Scale = new Vector2(scale.Value);
            internalSliderUpdate = false;
        }
    }
}
