// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Maps between <see cref="Ez2ConfigManager"/> bindables and <see cref="EzSkinJsonDocument"/>.
    /// </summary>
    public static class EzSkinJsonBridge
    {
        private static readonly MethodInfo get_bindable_method = typeof(Ez2ConfigManager).GetMethod(nameof(Ez2ConfigManager.GetBindable), BindingFlags.Public | BindingFlags.Instance)!;

        public static EzSkinJsonDocument Capture(Ez2ConfigManager config)
        {
            var document = new EzSkinJsonDocument();

            foreach (var setting in EzSkinJsonSettingCatalog.All)
            {
                if (tryCaptureSetting(config, setting, out string value))
                    document.Settings[setting.ToString()] = value;
            }

            return document;
        }

        public static void Apply(EzSkinJsonDocument document, Ez2ConfigManager config)
        {
            foreach (var pair in document.Settings)
            {
                if (!Enum.TryParse(pair.Key, out Ez2Setting setting))
                    continue;

                if (!EzSkinJsonSettingCatalog.Contains(setting))
                    continue;

                tryApplySetting(config, setting, pair.Value);
            }
        }

        public static string CreateNormalizedSnapshot(Ez2ConfigManager config) =>
            EzSkinJsonDocument.SerializeNormalized(Capture(config));

        private static bool tryCaptureSetting(Ez2ConfigManager config, Ez2Setting setting, out string value)
        {
            value = string.Empty;

            var bindable = getTypedBindable(config, setting);

            if (bindable == null)
                return false;

            value = getBindableValueString(bindable);
            return true;
        }

        private static string getBindableValueString(IBindable bindable)
        {
            if (bindable is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return bindable.ToString() ?? string.Empty;
        }

        private static void tryApplySetting(Ez2ConfigManager config, Ez2Setting setting, string value)
        {
            var bindable = getTypedBindable(config, setting);

            if (bindable is IParseable parseable)
                parseable.Parse(value, CultureInfo.InvariantCulture);
        }

        private static IBindable? getTypedBindable(Ez2ConfigManager config, Ez2Setting setting)
        {
            Type? valueType = getBindableValueType(config, setting);

            if (valueType == null)
                return null;

            return (IBindable?)get_bindable_method.MakeGenericMethod(valueType).Invoke(config, new object[] { setting });
        }

        private static Type? getBindableValueType(Ez2ConfigManager config, Ez2Setting setting)
        {
            // Access default type via a temporary bindable lookup through public getters for known settings.
            return setting switch
            {
                Ez2Setting.GameThemeName => typeof(EzEnumGameThemeName),
                Ez2Setting.StageName or Ez2Setting.NoteSetName => typeof(string),
                Ez2Setting.ManiaPseudo3DRotation or Ez2Setting.ColumnDim or Ez2Setting.ColumnBlur or Ez2Setting.ColumnWidth or Ez2Setting.SpecialFactor
                    or Ez2Setting.HitPosition or Ez2Setting.HitTargetFloatFixed or Ez2Setting.HitTargetAlpha or Ez2Setting.NoteHeightScaleToWidth
                    or Ez2Setting.NoteCornerRadius or Ez2Setting.ManiaHoldTailMaskGradientHeight or Ez2Setting.ManiaHoldTailAlpha or Ez2Setting.NoteTrackLineHeight => typeof(double),
                Ez2Setting.StagePanelEnabled or Ez2Setting.HitPositionGlobalEnable or Ez2Setting.ManiaLNGradientEnable or Ez2Setting.ColorSettingsEnabled => typeof(bool),
                Ez2Setting.ColumnWidthStyle => typeof(ColumnWidthStyle),
                Ez2Setting.ColumnTypeListSelect => typeof(int),
                Ez2Setting.ColumnTypeA or Ez2Setting.ColumnTypeB or Ez2Setting.ColumnTypeS or Ez2Setting.ColumnTypeE or Ez2Setting.ColumnTypeP => typeof(Colour4),
                Ez2Setting.ColumnTypeOf4K or Ez2Setting.ColumnTypeOf5K or Ez2Setting.ColumnTypeOf6K or Ez2Setting.ColumnTypeOf7K or Ez2Setting.ColumnTypeOf8K
                    or Ez2Setting.ColumnTypeOf9K or Ez2Setting.ColumnTypeOf10K or Ez2Setting.ColumnTypeOf12K or Ez2Setting.ColumnTypeOf14K
                    or Ez2Setting.ColumnTypeOf16K or Ez2Setting.ColumnTypeOf18K => typeof(string),
                _ => null,
            };
        }
    }
}
