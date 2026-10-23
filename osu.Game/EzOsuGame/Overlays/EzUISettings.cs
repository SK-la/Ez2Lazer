// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzUISettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_UI_SETTINGS_HEADER;

        [Resolved(CanBeNull = true)]
        private EzFontSettingsOverlay? fontOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            // 液态玻璃专属项：只在对应档位下出现，避免在亚克力/默认档留一堆无效滑条。
            SettingsItemV2[] glassItems =
            {
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_REFRACTION_DEPTH,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_REFRACTION_DEPTH_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassRefractionDepth),
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "glass", "refraction", "depth", "液态玻璃", "折射" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_REFRACTION_STRENGTH,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_REFRACTION_STRENGTH_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassRefractionStrength),
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "glass", "refraction", "liquid glass", "液态玻璃", "折射" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_CHROMATIC,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_CHROMATIC_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassChromatic),
                })
                {
                    Keywords = new[] { "glass", "chromatic", "dispersion", "液态玻璃", "色散" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_EDGE_HIGHLIGHT,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_EDGE_HIGHLIGHT_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassEdgeHighlight),
                })
                {
                    Keywords = new[] { "glass", "highlight", "rim", "液态玻璃", "高光" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_INNER_SHADOW,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_INNER_SHADOW_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassInnerShadow),
                })
                {
                    Keywords = new[] { "glass", "shadow", "inner", "液态玻璃", "内阴影" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_TINT_STRENGTH,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_TINT_STRENGTH_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassTintStrength),
                })
                {
                    Keywords = new[] { "glass", "tint", "colour", "液态玻璃", "染色" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.MATERIAL_GLASS_EDGE_SATURATION,
                    HintText = EzSettingsStrings.MATERIAL_GLASS_EDGE_SATURATION_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialGlassEdgeSaturation),
                })
                {
                    Keywords = new[] { "glass", "saturation", "edge", "液态玻璃", "饱和" }
                },
            };

            var materialMode = ezConfig.GetBindable<EzMaterialUiMode>(Ez2Setting.MaterialUiMode);

            materialMode.BindValueChanged(mode =>
            {
                foreach (var item in glassItems)
                    item.CanBeShown.Value = mode.NewValue == EzMaterialUiMode.LiquidGlass;
            }, true);

            Add(new SettingsItemV2(new FormEnumDropdown<EzMaterialUiMode>
            {
                Caption = EzSettingsStrings.MATERIAL_UI_MODE,
                HintText = EzSettingsStrings.MATERIAL_UI_MODE_TOOLTIP,
                Current = materialMode,
            })
            {
                Keywords = new[] { "acrylic", "glass", "blur", "song select", "ui", "毛玻璃", "液态玻璃" }
            });

            Add(new SettingsItemV2(new FormSliderBar<double>
            {
                Caption = EzSettingsStrings.MATERIAL_UI_BLUR,
                HintText = EzSettingsStrings.MATERIAL_UI_BLUR_TOOLTIP,
                Current = ezConfig.GetBindable<double>(Ez2Setting.MaterialUiBlur),
                KeyboardStep = 1,
            })
            {
                Keywords = new[] { "acrylic", "blur", "strength", "song select", "ui", "虚化" }
            });

            AddRange(glassItems);

            AddRange(new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = EzSettingsStrings.UI_FONT_MODIFY,
                    TooltipText = EzSettingsStrings.UI_FONT_MODIFY_TOOLTIP,
                    Action = () => fontOverlay?.ShowFromSettings(),
                    Keywords = new[] { "font", "typeface", "ui", "system", "字体" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzNotificationBehaviour>
                {
                    Caption = EzSettingsStrings.NOTIFICATION_BEHAVIOUR,
                    HintText = EzSettingsStrings.NOTIFICATION_BEHAVIOUR_TOOLTIP,
                    Current = ezConfig.GetBindable<EzNotificationBehaviour>(Ez2Setting.NotificationBehaviour),
                })
                {
                    Keywords = new[] { "notification", "toast", "alert", "sound", "ui", "gameplay" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzScreenshotAction>
                {
                    Caption = EzSettingsStrings.SCREENSHOT_ACTION,
                    Current = ezConfig.GetBindable<EzScreenshotAction>(Ez2Setting.ScreenshotAction),
                })
                {
                    Keywords = new[] { "screenshot", "clipboard", "capture", "image", "ui" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER,
                    HintText = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HideMainMenuOnlineBanner),
                })
                {
                    Keywords = new[] { "main menu", "banner", "news", "advertisement", "ui" }
                },
            });
        }
    }
}
