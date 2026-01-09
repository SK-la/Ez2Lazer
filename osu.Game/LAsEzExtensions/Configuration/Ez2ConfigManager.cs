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
using osu.Game.LAsEzExtensions.Select;
using osu.Game.Screens.SelectV2;
using osu.Game.Skinning.Components;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public class Ez2ConfigManager : IniConfigManager<Ez2Setting>, IGameplaySettings
    {
        protected override string Filename => "EzSkinSettings.ini"; // 以后可能改成 Ez2Settings.ini
        private readonly int[] commonKeyModes = { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };
        public float DefaultHitPosition = 180f;

        public static readonly Dictionary<int, EzColumnType[]> COLUMN_TYPE_CACHE = new Dictionary<int, EzColumnType[]>();
        public static readonly Dictionary<int, bool[]> IS_SPECIAL_CACHE = new Dictionary<int, bool[]>();

        private static readonly Dictionary<int, Ez2Setting> key_mode_to_column_color_setting = new Dictionary<int, Ez2Setting>
        {
            { 4, Ez2Setting.ColumnTypeOf4K },
            { 5, Ez2Setting.ColumnTypeOf5K },
            { 6, Ez2Setting.ColumnTypeOf6K },
            { 7, Ez2Setting.ColumnTypeOf7K },
            { 8, Ez2Setting.ColumnTypeOf8K },
            { 9, Ez2Setting.ColumnTypeOf9K },
            { 10, Ez2Setting.ColumnTypeOf10K },
            { 12, Ez2Setting.ColumnTypeOf12K },
            { 14, Ez2Setting.ColumnTypeOf14K },
            { 16, Ez2Setting.ColumnTypeOf16K },
            { 18, Ez2Setting.ColumnTypeOf18K },
        };

        private static readonly Dictionary<EzColumnType, Ez2Setting> column_type_to_setting = new Dictionary<EzColumnType, Ez2Setting>
        {
            [EzColumnType.A] = Ez2Setting.ColumnTypeA,
            [EzColumnType.B] = Ez2Setting.ColumnTypeB,
            [EzColumnType.S] = Ez2Setting.ColumnTypeS,
            [EzColumnType.E] = Ez2Setting.ColumnTypeE,
            [EzColumnType.P] = Ez2Setting.ColumnTypeP,
        };

        public Ez2ConfigManager(Storage storage)
            : base(storage)
        {
            initializeEvents();
        }

        protected override void InitialiseDefaults()
        {
            #region 皮肤类

            SetDefault(Ez2Setting.SelectedKeyMode, 4);
            SetDefault(Ez2Setting.ColumnWidthStyle, EzColumnWidthStyle.EzStyleProOnly);
            SetDefault(Ez2Setting.GlobalHitPosition, false);
            SetDefault(Ez2Setting.GlobalTextureName, 4);

            SetDefault(Ez2Setting.ColumnWidth, 60, 5, 400.0, 1.0);
            SetDefault(Ez2Setting.SpecialFactor, 1.2, 0.5, 2.0, 0.1);
            SetDefault(Ez2Setting.HitPosition, DefaultHitPosition, 0, 500, 1.0);
            SetDefault(Ez2Setting.VisualHitPosition, 0.0, -100, 100, 1.0);
            SetDefault(Ez2Setting.HitTargetFloatFixed, 6, 0, 10, 0.1);
            SetDefault(Ez2Setting.HitTargetAlpha, 0.6, 0, 1, 0.01);

            SetDefault(Ez2Setting.NoteSetName, "lucenteclat");
            SetDefault(Ez2Setting.StageName, "Celeste_Lumiere");
            SetDefault(Ez2Setting.GameThemeName, EzEnumGameThemeName.Celeste_Lumiere);
            SetDefault(Ez2Setting.NoteHeightScaleToWidth, 1, 0.1, 2, 0.1);
            SetDefault(Ez2Setting.NoteTrackLineHeight, 300, 0, 1000, 5.0);

            #endregion

            #region 列类型、着色系统

            SetDefault(Ez2Setting.ColorSettingsEnabled, true);
            SetDefault(Ez2Setting.ColumnBlur, 0.7, 0.0, 1, 0.01);
            SetDefault(Ez2Setting.ColumnDim, 0.7, 0.0, 1, 0.01);

            SetDefault(Ez2Setting.ColorSettingsEnabled, true);
            SetDefault(Ez2Setting.ColumnTypeA, Colour4.FromHex("#F5F5F5"));
            SetDefault(Ez2Setting.ColumnTypeB, Colour4.FromHex("#648FFF"));
            SetDefault(Ez2Setting.ColumnTypeS, Colour4.FromHex("#FF4A4A"));
            SetDefault(Ez2Setting.ColumnTypeE, Colour4.FromHex("#FF4A4A"));
            SetDefault(Ez2Setting.ColumnTypeP, Colour4.FromHex("#72FF72"));

            initializeColumnTypeDefaults();

            // Pre-populate caches for all common key modes to avoid lazy loading delays
            foreach (int keyMode in commonKeyModes)
            {
                GetColumnTypes(keyMode);
                GetSpecialColumnsBools(keyMode);
            }

            #endregion

            #region 定制皮肤编辑器

            // Mania 长按尾部相关（皮肤编辑器用，暂时只作为占位设置项）。
            SetDefault(Ez2Setting.ManiaHoldTailAlpha, 0.0, 0.0, 1.0, 0.01);
            SetDefault(Ez2Setting.ManiaHoldTailMaskGradientHeight, 0.0, 0.0, 50.0, 1.0);

            #endregion

            SetDefault(Ez2Setting.KpcDisplayMode, EzKpcDisplay.KpcDisplayMode.Numbers);
            SetDefault(Ez2Setting.XxySRFilter, false);
            SetDefault(Ez2Setting.KeySoundPreview, false);
            SetDefault(Ez2Setting.EzSelectCsMode, CsItemIds.ALL.First().Id);

            SetDefault(Ez2Setting.HitMode, EzMUGHitMode.EZ2AC);
            SetDefault(Ez2Setting.CustomHealthMode, EnumHealthMode.Lazer);
            SetDefault(Ez2Setting.CustomPoorHitResult, true);

            SetDefault(Ez2Setting.AccuracyCutoffS, 0.95, 0.95, 1, 0.005);
            SetDefault(Ez2Setting.AccuracyCutoffA, 0.9, 0.9, 1, 0.005);

            SetDefault(Ez2Setting.ScalingGameMode, ScalingGameMode.Mania);

            SetDefault(Ez2Setting.GameplayDisableCmdSpace, true);

        }

        #region 列类型管理

        private void initializeColumnTypeDefaults()
        {
            foreach (int keyMode in commonKeyModes)
            {
                if (key_mode_to_column_color_setting.TryGetValue(keyMode, out var setting))
                {
                    EzColumnType[] defaultTypes = getDefaultColumnTypes(keyMode);
                    SetDefault(setting, string.Join(",", defaultTypes));
                }
            }
        }

        private static EzColumnType[] getDefaultColumnTypes(int keyMode)
        {
            return Enumerable.Range(0, keyMode)
                             .Select(i => EzColumnTypeManager.GetColumnType(keyMode, i))
                             .ToArray();
        }

        public void SetColumnType(int keyMode, int columnIndex, EzColumnType colorType)
        {
            SetColumnType(keyMode, columnIndex, colorType.ToString());
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

                COLUMN_TYPE_CACHE.Remove(keyMode);
                IS_SPECIAL_CACHE.Remove(keyMode);
            }
            catch (NotSupportedException)
            {
            }
        }

        private static Ez2Setting getColumnTypeListSetting(int keyMode)
        {
            if (key_mode_to_column_color_setting.TryGetValue(keyMode, out var setting))
                return setting;

            throw new NotSupportedException($"不支持 {keyMode} 键位模式");
        }

        #endregion

        #region 公共方法

        public float GetTotalWidth(int keyMode)
        {
            double baseWidth = GetBindable<double>(Ez2Setting.ColumnWidth).Value;
            double specialFactor = GetBindable<double>(Ez2Setting.SpecialFactor).Value;
            float totalWidth = 0;
            int forMode = keyMode == 14 ? keyMode - 1 : keyMode;
            bool[] isSpecials = GetSpecialColumnsBools(keyMode);

            for (int i = 0; i < forMode; i++)
            {
                bool isSpecial = isSpecials[i];
                totalWidth += (float)(baseWidth * (isSpecial ? specialFactor : 1.0));
            }

            return totalWidth;
        }

        public Colour4 GetColumnColor(int keyMode, int columnIndex)
        {
            EzColumnType colorType = GetColumnType(keyMode, columnIndex);

            if (column_type_to_setting.TryGetValue(colorType, out var setting))
                return Get<Colour4>(setting);

            return Get<Colour4>(Ez2Setting.ColumnTypeA);
        }

        public IBindable<Colour4> GetColumnColorBindable(int keyMode, int columnIndex)
        {
            EzColumnType colorType = GetColumnType(keyMode, columnIndex);

            if (column_type_to_setting.TryGetValue(colorType, out var setting))
                return GetBindable<Colour4>(setting);

            return GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
        }

        public bool IsSpecialColumn(int keyMode, int columnIndex)
        {
            return GetColumnType(keyMode, columnIndex) == EzColumnType.S;
        }

        public bool[] GetSpecialColumnsBools(int keyMode)
        {
            if (IS_SPECIAL_CACHE.TryGetValue(keyMode, out bool[]? specials))
                return specials;

            EzColumnType[] types = GetColumnTypes(keyMode);
            bool[] result = new bool[keyMode];

            for (int i = 0; i < keyMode; i++)
            {
                result[i] = types[i] == EzColumnType.S;
            }

            IS_SPECIAL_CACHE[keyMode] = result;
            return result;
        }

        public EzColumnType GetColumnType(int keyMode, int columnIndex)
        {
            EzColumnType[] types = GetColumnTypes(keyMode);
            if (columnIndex < types.Length)
                return types[columnIndex];

            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
        }

        public EzColumnType[] GetColumnTypes(int keyMode)
        {
            if (COLUMN_TYPE_CACHE.TryGetValue(keyMode, out EzColumnType[]? types))
                return types;

            types = new EzColumnType[keyMode];

            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                string? columnColors = Get<string>(setting);

                if (!string.IsNullOrEmpty(columnColors))
                {
                    int start = 0;
                    int index = 0;

                    for (int i = 0; i <= columnColors.Length && index < keyMode; i++)
                    {
                        if (i == columnColors.Length || columnColors[i] == ',')
                        {
                            string part = columnColors.Substring(start, i - start).Trim();
                            if (!string.IsNullOrEmpty(part) && Enum.TryParse<EzColumnType>(part, out var t))
                                types[index] = t;
                            else
                                types[index] = EzColumnTypeManager.GetColumnType(keyMode, index);
                            index++;
                            start = i + 1;
                        }
                    }

                    // Fill remaining with defaults
                    for (int i = index; i < keyMode; i++)
                        types[i] = EzColumnTypeManager.GetColumnType(keyMode, i);
                }
                else
                {
                    for (int i = 0; i < keyMode; i++)
                        types[i] = EzColumnTypeManager.GetColumnType(keyMode, i);
                }
            }
            catch (NotSupportedException)
            {
                for (int i = 0; i < keyMode; i++)
                    types[i] = EzColumnTypeManager.GetColumnType(keyMode, i);
            }

            COLUMN_TYPE_CACHE[keyMode] = types;
            return types;
        }

        #endregion

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

        #region 事件发布

        // public event Action? OnPositionChanged;
        public event Action? OnNoteSizeChanged;
        public event Action? OnNoteColourChanged;

        private void initializeEvents()
        {
            var columnWidthBindable = GetBindable<double>(Ez2Setting.ColumnWidth);
            var specialFactorBindable = GetBindable<double>(Ez2Setting.SpecialFactor);
            var columnWidthStyleBindable = GetBindable<EzColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);

            columnWidthBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            specialFactorBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            columnWidthStyleBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());

            var colorSettingsEnabledBindable = GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            var colorABindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            var colorBBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            var colorSBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            var colorEBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            var colorPBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeP);

            colorSettingsEnabledBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorABindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorBBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorSBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorEBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorPBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
        }

        #endregion

        public new Bindable<T> GetBindable<T>(Ez2Setting setting)
        {
            return base.GetBindable<T>(setting);
        }

        public new void SetValue<T>(Ez2Setting lookup, T value)
        {
            base.SetValue(lookup, value);
        }

        public new void Save()
        {
            base.Save();
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;

        public int KeyMode;

        public int GetKeyMode()
        {
            return KeyMode;
        }

        public double ColumnTotalWidth;

        public double GetColumnTotalWidth()
        {
            return ColumnTotalWidth;
        }
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

    public enum Ez2Setting
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

        // Mania 长按尾部相关（EzSkinEditor 用）
        ManiaHoldTailAlpha,
        ManiaHoldTailMaskGradientHeight,

        GlobalTextureName,
        NoteSetName,
        StageName,
        GameThemeName,

        // 着色系统
        ColorSettingsEnabled,
        ColumnTypeOf4K,
        ColumnTypeOf5K,
        ColumnTypeOf6K,
        ColumnTypeOf7K,
        ColumnTypeOf8K,
        ColumnTypeOf9K,
        ColumnTypeOf10K,
        ColumnTypeOf12K,
        ColumnTypeOf14K,
        ColumnTypeOf16K,
        ColumnTypeOf18K,

        // 列类型
        // ColumnTypeBase = 500,
        ColumnTypeA,
        ColumnTypeB,
        ColumnTypeS,
        ColumnTypeE,
        ColumnTypeP,
        ColumnBlur,
        ColumnDim,

        // 其他设置
        KeySoundPreview,
        EzSelectCsMode,
        ScalingGameMode,
        AccuracyCutoffS,
        AccuracyCutoffA,
        HitMode,
        CustomHealthMode,
        CustomPoorHitResult,
        XxySRFilter,
        KpcDisplayMode,
        GameplayDisableCmdSpace

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
