// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.HUD;
using osu.Game.LAsEzExtensions.Online;
using osu.Game.Screens.SelectV2;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public class Ez2ConfigManager : IniConfigManager<Ez2Setting>, IGameplaySettings
    {
        protected override string Filename => "EzSkinSettings.ini";
        private readonly int[] commonKeyModes = { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };
        public float DefaultHitPosition = 180f;

        public static readonly Dictionary<int, EzColumnType[]> COLUMN_TYPE_CACHE = new Dictionary<int, EzColumnType[]>();
        public static readonly Dictionary<int, bool[]> IS_SPECIAL_CACHE = new Dictionary<int, bool[]>();

        private static readonly ConcurrentDictionary<int, KeyModeColumnData> runtime_column_data = new ConcurrentDictionary<int, KeyModeColumnData>();

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

        // Cache of bindables returned by GetBindable to avoid creating multiple instances
        private readonly object bindableCacheLock = new object();
        private readonly Dictionary<Ez2Setting, object> bindableCache = new Dictionary<Ez2Setting, object>();

        protected override void InitialiseDefaults()
        {
            #region 皮肤类

            SetDefault(Ez2Setting.ColumnTypeListSelect, 4);
            SetDefault(Ez2Setting.ColumnWidthStyle, ColumnWidthStyle.EzStyleProOnly);
            SetDefault(Ez2Setting.GlobalHitPosition, false);
            SetDefault(Ez2Setting.GlobalTextureName, 4);

            SetDefault(Ez2Setting.ColumnWidth, 60, 5, 400.0, 1.0);
            SetDefault(Ez2Setting.SpecialFactor, 1.2, 0.5, 2.0, 0.1);
            SetDefault(Ez2Setting.HitPosition, DefaultHitPosition, 0, 500, 1.0);
            SetDefault(Ez2Setting.HitTargetFloatFixed, 6, 0, 10, 0.1);
            SetDefault(Ez2Setting.HitTargetAlpha, 0.6, 0, 1, 0.01);

            SetDefault(Ez2Setting.NoteSetName, "lucenteclat");
            SetDefault(Ez2Setting.StageName, "Celeste_Lumiere");
            SetDefault(Ez2Setting.StagePanelEnabled, true);
            SetDefault(Ez2Setting.GameThemeName, EzEnumGameThemeName.Celeste_Lumiere);
            SetDefault(Ez2Setting.NoteHeightScaleToWidth, 1, 0.1, 10, 0.1);
            SetDefault(Ez2Setting.NoteTrackLineHeight, 300, 0, 1000, 5.0);

            #endregion

            #region 列类型、着色系统

            SetDefault(Ez2Setting.ColumnDim, 0.5, 0.0, 1, 0.01);
            SetDefault(Ez2Setting.ColumnBlur, 0.3, 0.0, 1, 0.01);

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

            SetDefault(Ez2Setting.AccuracyCutoffS, 0.95, 0.95, 1, 0.005);
            SetDefault(Ez2Setting.AccuracyCutoffA, 0.9, 0.9, 1, 0.005);

            SetDefault(Ez2Setting.ScalingGameMode, ScalingGameMode.Mania);

            SetDefault(Ez2Setting.GameplayDisableCmdSpace, true);
            SetDefault(Ez2Setting.AsioSampleRate, 48000);
            SetDefault(Ez2Setting.InputAudioLatencyTracker, false);

            SetDefault(Ez2Setting.KpcDisplayMode, KpcDisplayMode.BarChart);
            SetDefault(Ez2Setting.XxySRFilter, false);
            SetDefault(Ez2Setting.KeySoundPreviewMode, KeySoundPreviewMode.Off);
            SetDefault(Ez2Setting.EzSelectCsMode, "");
            initializeManiaDefaults();

            // 判定偏移修正（以毫秒计）
            SetDefault(Ez2Setting.OffsetPlusMania, 0.0, -200.0, 200.0, 1.0);
            SetDefault(Ez2Setting.OffsetPlusNonMania, 0.0, -200.0, 200.0, 1.0);

            // 服务器配置
            SetDefault(Ez2Setting.ServerPreset, ServerPreset.Official);
            SetDefault(Ez2Setting.CustomApiUrl, string.Empty);
            SetDefault(Ez2Setting.CustomWebsiteUrl, string.Empty);
            SetDefault(Ez2Setting.CustomClientId, string.Empty);
            SetDefault(Ez2Setting.CustomClientSecret, string.Empty);
            SetDefault(Ez2Setting.CustomSpectatorUrl, string.Empty);
            SetDefault(Ez2Setting.CustomMultiplayerUrl, string.Empty);
            SetDefault(Ez2Setting.CustomMetadataUrl, string.Empty);

            // 每个服务器对应的登录账号
            SetDefault(Ez2Setting.ServerOfficialUsername, string.Empty);
            SetDefault(Ez2Setting.ServerOfficialToken, string.Empty);
            SetDefault(Ez2Setting.ServerGuUsername, string.Empty);
            SetDefault(Ez2Setting.ServerGuToken, string.Empty);
            SetDefault(Ez2Setting.ServerManualUsername, string.Empty);
            SetDefault(Ez2Setting.ServerManualToken, string.Empty);
        }

        private void initializeManiaDefaults()
        {
            SetDefault(Ez2Setting.HitMode, EzEnumHitMode.Lazer);
            SetDefault(Ez2Setting.CustomHealthMode, EzEnumHealthMode.Lazer);
            SetDefault(Ez2Setting.CustomPoorHitResultBool, true);
            SetDefault(Ez2Setting.ManiaBarLinesBool, true);

            SetDefault(Ez2Setting.ManiaPseudo3DRotation, 0.0, 0.0, 75.0, 1.0);
            SetDefault(Ez2Setting.ManiaHoldTailAlpha, 1.0, 0.0, 1.0, 0.01);
            SetDefault(Ez2Setting.ManiaHoldTailMaskGradientHeight, 0.0, 0.0, 100.0, 1.0);
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

                buildKeyModeColumnDataFromSetting(keyMode);
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
            double baseWidth = Get<double>(Ez2Setting.ColumnWidth);
            double specialFactor = Get<double>(Ez2Setting.SpecialFactor);
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

        public Bindable<Colour4> GetColumnColorBindable(int keyMode, int columnIndex)
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

            // If runtime data exists, use it to build the bool[] quickly.
            if (runtime_column_data.TryGetValue(keyMode, out var data))
            {
                bool[] result = new bool[data.Length];
                int len = data.Length;
                int uptoMask = Math.Min(len, 64);
                // use mask for indices < 64
                for (int i = 0; i < uptoMask; i++)
                    result[i] = ((data.SpecialMask >> i) & 1UL) != 0UL;
                // for indices >= 64 fall back to types array
                for (int i = uptoMask; i < len; i++)
                    result[i] = data.Types[i] == (byte)EzColumnType.S;

                IS_SPECIAL_CACHE[keyMode] = result;
                return result;
            }

            buildKeyModeColumnDataFromSetting(keyMode);

            if (IS_SPECIAL_CACHE.TryGetValue(keyMode, out bool[]? after))
                return after;

            // final fallback: use default generation
            EzColumnType[] types = GetColumnTypes(keyMode);
            bool[] fallback = new bool[keyMode];
            for (int i = 0; i < keyMode; i++)
                fallback[i] = types[i] == EzColumnType.S;

            IS_SPECIAL_CACHE[keyMode] = fallback;
            return fallback;
        }

        public EzColumnType GetColumnType(int keyMode, int columnIndex)
        {
            // Try runtime cache first for fastest path
            if (runtime_column_data.TryGetValue(keyMode, out var data))
            {
                if (columnIndex < data.Length)
                    return (EzColumnType)data.Types[columnIndex];

                return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
            }

            // Fallback to existing array cache or build data lazily
            if (COLUMN_TYPE_CACHE.TryGetValue(keyMode, out EzColumnType[]? types))
            {
                if (columnIndex < types.Length)
                    return types[columnIndex];

                return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
            }

            buildKeyModeColumnDataFromSetting(keyMode);

            if (runtime_column_data.TryGetValue(keyMode, out data))
            {
                if (columnIndex < data.Length)
                    return (EzColumnType)data.Types[columnIndex];
            }

            // final fallback
            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
        }

        public EzColumnType[] GetColumnTypes(int keyMode)
        {
            if (COLUMN_TYPE_CACHE.TryGetValue(keyMode, out EzColumnType[]? types))
                return types;

            // If runtime exists, materialize to EzColumnType[] for backward compatibility
            if (runtime_column_data.TryGetValue(keyMode, out var data))
            {
                var arr = new EzColumnType[data.Length];
                for (int i = 0; i < data.Length; i++)
                    arr[i] = (EzColumnType)data.Types[i];

                COLUMN_TYPE_CACHE[keyMode] = arr;
                return arr;
            }

            buildKeyModeColumnDataFromSetting(keyMode);

            if (COLUMN_TYPE_CACHE.TryGetValue(keyMode, out var after))
                return after;

            // final fallback to defaults
            var def = new EzColumnType[keyMode];
            for (int i = 0; i < keyMode; i++)
                def[i] = EzColumnTypeManager.GetColumnType(keyMode, i);

            COLUMN_TYPE_CACHE[keyMode] = def;
            return def;
        }

        // New helper: build runtime compact data from the stored setting string and populate public caches.
        private KeyModeColumnData buildKeyModeColumnDataFromSetting(int keyMode)
        {
            var setting = getColumnTypeListSetting(keyMode);
            string? columnColors = Get<string>(setting);

            byte[] typesBytes = new byte[keyMode];

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
                            typesBytes[index] = (byte)t;
                        else
                            typesBytes[index] = (byte)EzColumnTypeManager.GetColumnType(keyMode, index);

                        index++;
                        start = i + 1;
                    }
                }

                // Fill remaining with defaults
                for (int i = 0; i < keyMode; i++)
                {
                    if (typesBytes[i] == 0 && i >= columnColors.Split(',').Length)
                        typesBytes[i] = (byte)EzColumnTypeManager.GetColumnType(keyMode, i);
                }
            }
            else
            {
                for (int i = 0; i < keyMode; i++)
                    typesBytes[i] = (byte)EzColumnTypeManager.GetColumnType(keyMode, i);
            }

            // compute mask (only for indices < 64)
            ulong mask = 0;
            int upto = Math.Min(keyMode, 64);

            for (int i = 0; i < upto; i++)
            {
                if (typesBytes[i] == (byte)EzColumnType.S)
                    mask |= (1UL << i);
            }

            var data = new KeyModeColumnData(typesBytes, mask);
            runtime_column_data[keyMode] = data;

            // Also populate public caches for backward compatibility
            var enumArr = new EzColumnType[keyMode];
            bool[] boolArr = new bool[keyMode];

            for (int i = 0; i < keyMode; i++)
            {
                enumArr[i] = (EzColumnType)typesBytes[i];
                boolArr[i] = (i < 64) ? (((mask >> i) & 1UL) != 0UL) : (typesBytes[i] == (byte)EzColumnType.S);
            }

            COLUMN_TYPE_CACHE[keyMode] = enumArr;
            IS_SPECIAL_CACHE[keyMode] = boolArr;

            return data;
        }

        #endregion

        #region 事件发布

        // public event Action? OnPositionChanged;
        public event Action? OnNoteSizeChanged;
        public event Action? OnNoteColourChanged;

        private void initializeEvents()
        {
            var columnWidthBindable = GetBindable<double>(Ez2Setting.ColumnWidth);
            var specialFactorBindable = GetBindable<double>(Ez2Setting.SpecialFactor);
            var columnWidthStyleBindable = GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
            var noteHeightScaleBindable = GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth);
            var holdTailMaskHeightBindable = GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight);

            columnWidthBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            specialFactorBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            columnWidthStyleBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            noteHeightScaleBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            holdTailMaskHeightBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());

            var holdTailAlphaBindable = GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha);
            var colorSettingsEnabledBindable = GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);

            var colorABindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            var colorBBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            var colorSBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            var colorEBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            var colorPBindable = GetBindable<Colour4>(Ez2Setting.ColumnTypeP);

            holdTailAlphaBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
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
            lock (bindableCacheLock)
            {
                if (bindableCache.TryGetValue(setting, out object? existing))
                    return (Bindable<T>)existing;

                var b = base.GetBindable<T>(setting);
                bindableCache[setting] = b!;
                return b;
            }
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

        // New compact struct placed inside the class for clarity and correct accessibility.
        // This stores compact runtime representation: a byte[] of EzColumnType values and a 64-bit mask for 'S' columns.
        private readonly struct KeyModeColumnData
        {
            public readonly byte[] Types;
            public readonly ulong SpecialMask;

            public KeyModeColumnData(byte[] types, ulong specialMask)
            {
                Types = types;
                SpecialMask = specialMask;
            }

            public int Length => Types.Length;
        }
    }

    public enum Ez2Setting
    {
        // 界面设置
        KeySoundPreviewMode,
        XxySRFilter,
        KpcDisplayMode,

        ColumnTypeListSelect,
        EzSelectCsMode,

        // 全局开关
        ScalingGameMode,
        AccuracyCutoffS,
        AccuracyCutoffA,
        ColumnWidthStyle,
        GlobalHitPosition,

        // 皮肤设置
        ColumnWidth,
        SpecialFactor,

        // Ez专属
        HitPosition,
        HitTargetFloatFixed,
        HitTargetAlpha,
        NoteHeightScaleToWidth,
        NoteTrackLineHeight,
        NoteSetName,
        StageName,
        StagePanelEnabled,

        GlobalTextureName,
        GameThemeName,

        // Mania 长按尾部相关（EzSkinEditor 用）
        ManiaPseudo3DRotation,
        ManiaHoldTailAlpha,
        ManiaHoldTailMaskGradientHeight,

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
        ColumnTypeA,
        ColumnTypeB,
        ColumnTypeS,
        ColumnTypeE,
        ColumnTypeP,
        ColumnBlur,
        ColumnDim,

        // 音频相关
        AsioSampleRate,
        InputAudioLatencyTracker,

        // 来自拉取
        GameplayDisableCmdSpace,

        // Mania游戏专属设置
        // 判定偏移修正（以毫秒计）
        OffsetPlusMania,
        OffsetPlusNonMania,

        HitMode,
        CustomHealthMode,
        CustomPoorHitResultBool,
        ManiaBarLinesBool,

        // 服务器配置
        ServerPreset, // 服务器预设选项

        CustomApiUrl,
        CustomWebsiteUrl,
        CustomClientId,
        CustomClientSecret,
        CustomSpectatorUrl,
        CustomMultiplayerUrl,
        CustomMetadataUrl,

        // 各服务器对应的登录凭证
        ServerOfficialUsername,
        ServerOfficialToken,
        ServerGuUsername,
        ServerGuToken,
        ServerManualUsername,
        ServerManualToken,
    }

    public enum EzColumnType : byte
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
