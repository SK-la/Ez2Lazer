// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_GAMEPLAY_SETTINGS_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.STORYBOARD_VIDEO_AUTO_SIZE,
                    HintText = EzSettingsStrings.STORYBOARD_VIDEO_AUTO_SIZE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.StoryboardAutoVideoSize),
                })
                {
                    Keywords = new[] { "storyboard", "video", "size", "auto", "autosize", "ui" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SKIP_WITH_GAMEPLAY_KEYS,
                    HintText = EzSettingsStrings.SKIP_WITH_GAMEPLAY_KEYS_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.SkipWithGameplayKeys),
                })
                {
                    Keywords = new[] { "skip", "gameplay", "key", "mania", "std" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME,
                    HintText = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HitObjectLifetimeUsesOwnTime),
                })
                {
                    Keywords = new[] { "ez", "timing", "lifetime", "hitobject" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.TURBO_MODE,
                    HintText = EzSettingsStrings.TURBO_MODE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode),
                })
                {
                    Keywords = new[] { "turbo", "performance", "fps", "frame rate", "low spec", "极速", "帧数" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.FLOW_MODE,
                    HintText = EzSettingsStrings.FLOW_MODE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.FlowMode),
                })
                {
                    Keywords = new[] { "flow", "zen", "心流", "results", "ranking", "song select" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.GAMEPLAY_DIVISOR_NOTE_COLOURING,
                    HintText = EzSettingsStrings.GAMEPLAY_DIVISOR_NOTE_COLOURING_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.GameplayDivisorNoteColouring),
                })
                {
                    Keywords = new[] { "colour", "divisor", "mania", "note", "gameplay" }
                },
            });
        }
    }

    public partial class EzWeatherSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.WEATHER_ENVIRONMENT_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            Bindable<WeatherEffect> backgroundEffect = ezConfig.GetBindable<WeatherEffect>(Ez2Setting.BackgroundWeatherEffect);
            Bindable<WeatherEffect> playfieldEffect = ezConfig.GetBindable<WeatherEffect>(Ez2Setting.PlayfieldWeatherEffect);

            SettingsItemV2 backgroundIntensity = createWeatherSlider(
                EzSettingsStrings.BACKGROUND_WEATHER_INTENSITY,
                ezConfig.GetBindable<double>(Ez2Setting.BackgroundWeatherIntensity),
                0.05f,
                "background", "intensity", "strength");
            SettingsItemV2 playfieldIntensity = createWeatherSlider(
                EzSettingsStrings.PLAYFIELD_WEATHER_INTENSITY,
                ezConfig.GetBindable<double>(Ez2Setting.PlayfieldWeatherIntensity),
                0.05f,
                "playfield", "intensity", "strength");

            SettingsItemV2 mainMenuWeather = new SettingsItemV2(new FormCheckBox
            {
                Caption = EzSettingsStrings.MAIN_MENU_WEATHER_ENABLED,
                HintText = EzSettingsStrings.MAIN_MENU_WEATHER_ENABLED_TOOLTIP,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.MainMenuWeatherEnabled),
            })
            {
                Keywords = new[] { "main menu", "background", "weather" },
            };

            SettingsItemV2 backgroundAnimationSpeed = createWeatherSlider(
                EzSettingsStrings.BACKGROUND_WEATHER_ANIMATION_SPEED,
                ezConfig.GetBindable<double>(Ez2Setting.BackgroundWeatherAnimationSpeed),
                0.1f,
                "background", "animation", "speed");
            SettingsItemV2 playfieldAnimationSpeed = createWeatherSlider(
                EzSettingsStrings.PLAYFIELD_WEATHER_ANIMATION_SPEED,
                ezConfig.GetBindable<double>(Ez2Setting.PlayfieldWeatherAnimationSpeed),
                0.1f,
                "playfield", "animation", "speed");

            var backgroundTuningItems = createWeatherTuningItems(ezConfig, EzSettingsStrings.BACKGROUND_WEATHER_SCOPE);
            var playfieldTuningItems = createWeatherTuningItems(ezConfig, EzSettingsStrings.PLAYFIELD_WEATHER_SCOPE);

            backgroundEffect.BindValueChanged(_ => updateVisibility(), true);
            playfieldEffect.BindValueChanged(_ => updateVisibility(), true);

            var settingsItems = new List<Drawable>
            {
                new SettingsItemV2(new EzWeatherEffectDropdown
                {
                    Caption = EzSettingsStrings.BACKGROUND_WEATHER_EFFECT,
                    HintText = EzSettingsStrings.BACKGROUND_WEATHER_EFFECT_TOOLTIP,
                    Current = backgroundEffect,
                })
                {
                    Keywords = new[] { "weather", "background", "rain", "underwater", "heat" },
                },
                backgroundIntensity,
                mainMenuWeather,
                backgroundAnimationSpeed,
            };

            foreach (var group in backgroundTuningItems)
                settingsItems.AddRange(group.items);

            settingsItems.AddRange(new Drawable[]
            {
                new SettingsItemV2(new EzWeatherEffectDropdown
                {
                    Caption = EzSettingsStrings.PLAYFIELD_WEATHER_EFFECT,
                    HintText = EzSettingsStrings.PLAYFIELD_WEATHER_EFFECT_TOOLTIP,
                    Current = playfieldEffect,
                })
                {
                    Keywords = new[] { "weather", "playfield", "rain", "underwater", "heat" },
                },
                playfieldIntensity,
                playfieldAnimationSpeed,
            });

            foreach (var group in playfieldTuningItems)
                settingsItems.AddRange(group.items);
            AddRange(settingsItems);

            void updateVisibility()
            {
                bool backgroundEnabled = backgroundEffect.Value != WeatherEffect.None;
                bool playfieldEnabled = playfieldEffect.Value != WeatherEffect.None;

                backgroundIntensity.CanBeShown.Value = backgroundEnabled;
                mainMenuWeather.CanBeShown.Value = backgroundEnabled;
                backgroundAnimationSpeed.CanBeShown.Value = backgroundEnabled;
                playfieldIntensity.CanBeShown.Value = playfieldEnabled;
                playfieldAnimationSpeed.CanBeShown.Value = playfieldEnabled;

                updateWeatherTuningVisibility(backgroundTuningItems, backgroundEffect.Value, WeatherEffect.None);
                updateWeatherTuningVisibility(playfieldTuningItems, playfieldEffect.Value, backgroundEffect.Value);
            }
        }

        private static (WeatherEffect effect, SettingsItemV2[] items)[] createWeatherTuningItems(Ez2ConfigManager ezConfig, LocalisableString scope)
        {
            return new (WeatherEffect effect, SettingsItemV2[] items)[]
            {
                (WeatherEffect.DesertHeat, new SettingsItemV2[]
                {
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_DESERT_HEAT_AMPLITUDE, ezConfig.GetBindable<double>(Ez2Setting.WeatherDesertHeatAmplitude), 0.1f, "desert heat", "amplitude"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_DESERT_HEAT_SCALE, ezConfig.GetBindable<double>(Ez2Setting.WeatherDesertHeatScale), 0.1f, "desert heat", "scale"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_DESERT_HEAT_TINT, ezConfig.GetBindable<double>(Ez2Setting.WeatherDesertHeatTint), 0.01f, "desert heat", "tint"),
                }),
                (WeatherEffect.Underwater, new SettingsItemV2[]
                {
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_UNDERWATER_SCALE, ezConfig.GetBindable<double>(Ez2Setting.WeatherUnderwaterScale), 0.1f, "underwater", "scale"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_UNDERWATER_BRIGHTNESS, ezConfig.GetBindable<double>(Ez2Setting.WeatherUnderwaterBrightness), 0.05f, "underwater", "brightness"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_UNDERWATER_TINT, ezConfig.GetBindable<double>(Ez2Setting.WeatherUnderwaterTint), 0.01f, "underwater", "tint"),
                }),
                (WeatherEffect.OvercastRain, new SettingsItemV2[]
                {
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_OVERCAST_RAIN_DENSITY, ezConfig.GetBindable<double>(Ez2Setting.WeatherOvercastRainDensity), 0.01f, "rain", "density"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_OVERCAST_RAIN_WIND, ezConfig.GetBindable<double>(Ez2Setting.WeatherOvercastRainWind), 0.05f, "rain", "wind"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_OVERCAST_RAIN_CLOUDINESS, ezConfig.GetBindable<double>(Ez2Setting.WeatherOvercastRainCloudiness), 0.01f, "rain", "cloudiness"),
                }),
                (WeatherEffect.UnderwaterLightCurtain, new SettingsItemV2[]
                {
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_LIGHT_CURTAIN_SCALE, ezConfig.GetBindable<double>(Ez2Setting.WeatherLightCurtainScale), 0.1f, "light curtain", "scale"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_LIGHT_CURTAIN_BRIGHTNESS, ezConfig.GetBindable<double>(Ez2Setting.WeatherLightCurtainBrightness), 0.05f, "light curtain", "brightness"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_LIGHT_CURTAIN_FALLOFF, ezConfig.GetBindable<double>(Ez2Setting.WeatherLightCurtainFalloff), 0.1f, "light curtain", "falloff"),
                    createScopedWeatherSlider(scope, EzSettingsStrings.WEATHER_LIGHT_CURTAIN_SWAY, ezConfig.GetBindable<double>(Ez2Setting.WeatherLightCurtainSway), 0.01f, "light curtain", "sway"),
                }),
            };
        }

        private static void updateWeatherTuningVisibility((WeatherEffect effect, SettingsItemV2[] items)[] groups, WeatherEffect selectedEffect, WeatherEffect effectToHide)
        {
            foreach (var group in groups)
            {
                bool visible = selectedEffect == group.effect && group.effect != effectToHide;

                foreach (SettingsItemV2 item in group.items)
                    item.CanBeShown.Value = visible;
            }
        }

        private static SettingsItemV2 createScopedWeatherSlider(LocalisableString scope, LocalisableString caption, Bindable<double> current, float keyboardStep, params string[] keywords)
        {
            LocalisableString scopedCaption = LocalisableString.Interpolate($"{scope} · {caption}");
            return createWeatherSlider(scopedCaption, current, keyboardStep, keywords);
        }

        private static SettingsItemV2 createWeatherSlider(LocalisableString caption, Bindable<double> current, float keyboardStep, params string[] keywords) => new SettingsItemV2(new FormSliderBar<double>
        {
            Caption = caption,
            Current = current,
            KeyboardStep = keyboardStep,
        })
        {
            Keywords = keywords,
        };
    }

    internal partial class EzWeatherEffectDropdown : FormEnumDropdown<WeatherEffect>
    {
        protected override LocalisableString GenerateItemText(WeatherEffect item) => item switch
        {
            WeatherEffect.None => EzSettingsStrings.WEATHER_EFFECT_NONE,
            WeatherEffect.DesertHeat => EzSettingsStrings.WEATHER_EFFECT_DESERT_HEAT,
            WeatherEffect.Underwater => EzSettingsStrings.WEATHER_EFFECT_UNDERWATER,
            WeatherEffect.OvercastRain => EzSettingsStrings.WEATHER_EFFECT_OVERCAST_RAIN,
            WeatherEffect.UnderwaterLightCurtain => EzSettingsStrings.WEATHER_EFFECT_LIGHT_CURTAIN,
            _ => base.GenerateItemText(item),
        };
    }
}
