// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Screens.LAsEzExtensions;
using osu.Game.Skinning;

namespace osu.Game.Screens
{
    public class EzSkinSettingsManager : IniConfigManager<EzSkinSetting>, IGameplaySettings
    {
        protected override string Filename => "EzSkinSettings.ini";
        private readonly int[] commonKeyModes = { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        private Dictionary<int, EzColumnType[]> columnTypeCache = new Dictionary<int, EzColumnType[]>();

        private static readonly Dictionary<int, EzSkinSetting> key_mode_to_column_color_setting = new Dictionary<int, EzSkinSetting>
        {
            { 4, EzSkinSetting.ColumnColor4K },
            { 5, EzSkinSetting.ColumnColor5K },
            { 6, EzSkinSetting.ColumnColor6K },
            { 7, EzSkinSetting.ColumnColor7K },
            { 8, EzSkinSetting.ColumnColor8K },
            { 9, EzSkinSetting.ColumnColor9K },
            { 10, EzSkinSetting.ColumnColor10K },
            { 12, EzSkinSetting.ColumnColor12K },
            { 14, EzSkinSetting.ColumnColor14K },
            { 16, EzSkinSetting.ColumnColor16K },
            { 18, EzSkinSetting.ColumnColor18K },
        };

        private static readonly Dictionary<string, EzSkinSetting> column_type_to_setting = new Dictionary<string, EzSkinSetting>
        {
            [nameof(EzColumnType.A)] = EzSkinSetting.ColumnTypeA,
            [nameof(EzColumnType.B)] = EzSkinSetting.ColumnTypeB,
            [nameof(EzColumnType.S)] = EzSkinSetting.ColumnTypeS,
            [nameof(EzColumnType.E)] = EzSkinSetting.ColumnTypeE,
            [nameof(EzColumnType.P)] = EzSkinSetting.ColumnTypeP,
        };

        public EzSkinSettingsManager(Storage storage)
            : base(storage)
        {
            initializeEvents();
        }

        protected override void InitialiseDefaults()
        {
            SetDefault(EzSkinSetting.SelectedKeyMode, 4);
            SetDefault(EzSkinSetting.ColumnWidthStyle, EzColumnWidthStyle.EzStyleProOnly);
            SetDefault(EzSkinSetting.GlobalHitPosition, false);
            SetDefault(EzSkinSetting.GlobalTextureName, 4);

            SetDefault(EzSkinSetting.ColumnWidth, 60, 5, 400.0, 1.0);
            SetDefault(EzSkinSetting.SpecialFactor, 1.2, 0.5, 2.0, 0.1);
            SetDefault(EzSkinSetting.HitPosition, 180, 0, 500, 1.0);
            SetDefault(EzSkinSetting.VisualHitPosition, 0.0, -100, 100, 1.0);
            SetDefault(EzSkinSetting.HitTargetFloatFixed, 6, 0, 10, 0.1);
            SetDefault(EzSkinSetting.HitTargetAlpha, 0.6, 0, 1, 0.01);

            SetDefault(EzSkinSetting.NoteSetName, "evolve");
            SetDefault(EzSkinSetting.StageName, "JIYU");
            SetDefault(EzSkinSetting.NoteHeightScaleToWidth, 1, 0.1, 2, 0.1);
            SetDefault(EzSkinSetting.NoteTrackLineHeight, 300, 0, 1000, 5.0);

            SetDefault(EzSkinSetting.ColorSettingsEnabled, true);
            SetDefault(EzSkinSetting.ColumnBlur,  0.7, 0.01, 1, 0.01);
            SetDefault(EzSkinSetting.ColumnDim,  0.7, 0.01, 1, 0.01);
            SetDefault(EzSkinSetting.ColumnTypeA, Colour4.FromHex("#F5F5F5"));
            SetDefault(EzSkinSetting.ColumnTypeB, Colour4.FromHex("#648FFF"));
            SetDefault(EzSkinSetting.ColumnTypeS, Colour4.FromHex("#FF4A4A"));
            SetDefault(EzSkinSetting.ColumnTypeE, Colour4.FromHex("#FF4A4A"));
            SetDefault(EzSkinSetting.ColumnTypeP, Colour4.FromHex("#72FF72"));

            SetDefault(EzSkinSetting.ColorSettingsEnabled, true);
            SetDefault(EzSkinSetting.ColumnTypeA, Colour4.FromHex("#F5F5F5"));
            SetDefault(EzSkinSetting.ColumnTypeB, Colour4.FromHex("#648FFF"));
            SetDefault(EzSkinSetting.ColumnTypeS, Colour4.FromHex("#FF4A4A"));
            SetDefault(EzSkinSetting.ColumnTypeE, Colour4.FromHex("#FF4A4A"));
            SetDefault(EzSkinSetting.ColumnTypeP, Colour4.FromHex("#72FF72"));
            initializeColumnTypeDefaults();
        }

        private void initializeColumnTypeDefaults()
        {
            foreach (int keyMode in commonKeyModes)
            {
                if (key_mode_to_column_color_setting.TryGetValue(keyMode, out var setting))
                {
                    string[] defaultTypes = getDefaultColumnTypes(keyMode);
                    SetDefault(setting, string.Join(",", defaultTypes));
                }
            }
        }

        private static EzSkinSetting getColumnTypeListSetting(int keyMode)
        {
            if (key_mode_to_column_color_setting.TryGetValue(keyMode, out var setting))
                return setting;

            throw new NotSupportedException($"不支持 {keyMode} 键位模式");
        }

        private static string[] getDefaultColumnTypes(int keyMode)
        {
            return Enumerable.Range(0, keyMode)
                .Select(i => EzColumnTypeManager.GetColumnType(keyMode, i))
                .ToArray();
        }

        private EzColumnType[] getColumnTypes(int keyMode)
        {
            if (columnTypeCache.TryGetValue(keyMode, out EzColumnType[] types))
                return types;

            string[] stringTypes;

            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                string? columnColors = Get<string>(setting);
                stringTypes = columnColors?.Split(',') ?? Array.Empty<string>();
            }
            catch (NotSupportedException)
            {
                stringTypes = Array.Empty<string>();
            }

            types = stringTypes.Select(s =>
            {
                if (string.IsNullOrEmpty(s)) return EzColumnType.A;

                return Enum.TryParse<EzColumnType>(s, out var t) ? t : EzColumnType.A;
            }).ToArray();

            columnTypeCache[keyMode] = types;
            return types;
        }

        public string GetColumnType(int keyMode, int columnIndex)
        {
            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                string? columnColors = Get<string>(setting);
                string[] types = columnColors?.Split(',') ?? Array.Empty<string>();

                if (columnIndex < types.Length && !string.IsNullOrEmpty(types[columnIndex]))
                    return types[columnIndex];
            }
            catch (NotSupportedException)
            {
            }

            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
        }

        public bool IsSpecialColumn(int keyMode, int columnIndex)
        {
            EzColumnType[] types = getColumnTypes(keyMode);
            if (columnIndex < types.Length)
                return types[columnIndex] == EzColumnType.S;

            return EzColumnTypeManager.IsSpecialColumn(keyMode, columnIndex);
        }

        public void SetColumnType(int keyMode, int columnIndex, string colorType)
        {
            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                string? currentConfig = Get<string>(setting);
                string[] types = !string.IsNullOrEmpty(currentConfig)
                    ? currentConfig.Split(',')
                    : new string[keyMode];

                if (types.Length <= columnIndex)
                {
                    Array.Resize(ref types, Math.Max(keyMode, columnIndex + 1));
                }

                types[columnIndex] = colorType.Trim();
                SetValue(setting, string.Join(",", types));
                columnTypeCache.Remove(keyMode);
            }
            catch (NotSupportedException)
            {
            }
        }

        public Colour4 GetColumnColor(int keyMode, int columnIndex)
        {
            string colorType = GetColumnType(keyMode, columnIndex);

            if (column_type_to_setting.TryGetValue(colorType, out var setting))
                return Get<Colour4>(setting);

            return Get<Colour4>(EzSkinSetting.ColumnTypeA);
        }

        public IBindable<Colour4> GetColumnColorBindable(int keyMode, int columnIndex)
        {
            string colorType = GetColumnType(keyMode, columnIndex);

            if (column_type_to_setting.TryGetValue(colorType, out var setting))
                return GetBindable<Colour4>(setting);

            return GetBindable<Colour4>(EzSkinSetting.ColumnTypeA);
        }

        // public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex)
        // {
        //     var result = new Bindable<Vector2>();
        //
        //     var columnWidthBindable = GetBindable<double>(EzSkinSetting.ColumnWidth);
        //     var specialFactorBindable = GetBindable<double>(EzSkinSetting.SpecialFactor);
        //     var heightScaleBindable = GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);
        //
        //     void updateNoteSize()
        //     {
        //         bool isSpecialColumn = GetColumnType(keyMode, columnIndex) == "S";
        //         double baseWidth = columnWidthBindable.Value;
        //         double specialFactor = specialFactorBindable.Value;
        //         double heightScale = heightScaleBindable.Value;
        //
        //         float x = (float)(baseWidth * (isSpecialColumn ? specialFactor : 1.0));
        //         float y = (float)(heightScale);
        //         result.Value = new Vector2(x, y);
        //     }
        //
        //     columnWidthBindable.BindValueChanged(e =>
        //     {
        //         Logger.Log($"ColumnWidth changed: {e.NewValue}");
        //         updateNoteSize();
        //     });
        //     specialFactorBindable.BindValueChanged(_ => updateNoteSize());
        //     heightScaleBindable.BindValueChanged(_ => updateNoteSize());
        //
        //     updateNoteSize();
        //
        //     return result;
        // }

        public new Bindable<T> GetBindable<T>(EzSkinSetting setting)
        {
            return base.GetBindable<T>(setting);
        }

        // public event Action? OnPositionChanged;
        // public event Action? OnNoteSizeChanged;

        private void initializeEvents()
        {
            // GetBindable<double>(EzSkinSetting.HitPosition).BindValueChanged(_ => OnPositionChanged?.Invoke(), true);
            // GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth).BindValueChanged(_ => updateAllNoteSizes(), true);
            // GetBindable<double>(EzSkinSetting.ColumnWidth).BindValueChanged(_ => updateAllNoteSizes(), true);
            // GetBindable<double>(EzSkinSetting.SpecialFactor).BindValueChanged(_ => updateAllNoteSizes(), true);
        }

        // private void updateAllNoteSizes()
        // {
        //     OnNoteSizeChanged?.Invoke();
        // }

        public new void SetValue<T>(EzSkinSetting lookup, T value)
        {
            base.SetValue(lookup, value);
        }

        public new void Save()
        {
            base.Save();
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;
    }

    public enum EzColumnWidthStyle
    {
        [Description("EzStylePro Only")]
        EzStyleProOnly,

        [Description("Global (全局)")]
        GlobalWidth,

        [Description("Global Total (全局总宽度)")]
        GlobalTotalWidth,
    }

    public enum EzSkinSetting
    {
        SelectedKeyMode,

        // 全局开关
        ColumnWidthStyle,
        GlobalHitPosition, //TODO:未来改成下拉栏，补充虚拟判定线

        // 全局设置
        ColumnWidth,
        SpecialFactor,

        // Ez专属皮肤设置
        HitPosition,
        HitTargetFloatFixed,
        HitTargetAlpha,
        VisualHitPosition,
        NoteHeightScaleToWidth,
        NoteTrackLineHeight,
        GlobalTextureName,
        NoteSetName,
        StageName,

        // 着色系统
        ColorSettingsEnabled,
        ColumnColor4K,
        ColumnColor5K,
        ColumnColor6K,
        ColumnColor7K,
        ColumnColor8K,
        ColumnColor9K,
        ColumnColor10K,
        ColumnColor12K,
        ColumnColor14K,
        ColumnColor16K,
        ColumnColor18K,

        // 列类型
        // ColumnTypeBase = 500,
        ColumnTypeA,
        ColumnTypeB,
        ColumnTypeS,
        ColumnTypeE,
        ColumnTypeP,
        ColumnBlur,
        ColumnDim
    }

    public enum EzColumnType
    {
        A,
        B,
        S,
        E,
        P
    }

    public static class EzConstants
    {
        public const string COLUMN_TYPE_A = nameof(EzColumnType.A);
        public const string COLUMN_TYPE_B = nameof(EzColumnType.B);
        public const string COLUMN_TYPE_S = nameof(EzColumnType.S);
        public const string COLUMN_TYPE_E = nameof(EzColumnType.E);
        public const string COLUMN_TYPE_P = nameof(EzColumnType.P);
    }
}
