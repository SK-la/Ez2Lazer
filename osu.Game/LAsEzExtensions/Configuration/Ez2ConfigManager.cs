// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
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

        private static readonly string[] column_type_names =
        {
            nameof(EzColumnType.A),
            nameof(EzColumnType.B),
            nameof(EzColumnType.S),
            nameof(EzColumnType.E),
            nameof(EzColumnType.P),
        };

        public Ez2ConfigManager(Storage storage)
            : base(storage)
        {
            initializeEvents();
        }

        // Save debounce machinery: schedule delayed Save() to coalesce rapid writes.
        private readonly object saveLock = new object();
        private System.Threading.Timer? saveTimer;
        private const int save_debounce_ms = 300;

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
            var defaults = new EzColumnType[keyMode];

            for (int i = 0; i < keyMode; i++)
                defaults[i] = EzColumnTypeManager.GetColumnType(keyMode, i);

            return defaults;
        }

        public void SetColumnType(int keyMode, int columnIndex, EzColumnType colorType)
        {
            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                KeyModeColumnData current = getOrBuildKeyModeColumnData(keyMode);
                byte newValue = (byte)colorType;

                if (columnIndex < current.Length && current.Types[columnIndex] == newValue)
                    return;

                int targetLength = Math.Max(current.Length, Math.Max(keyMode, columnIndex + 1));
                byte[] updatedTypes;

                if (targetLength == current.Length)
                {
                    updatedTypes = (byte[])current.Types.Clone();
                }
                else
                {
                    updatedTypes = new byte[targetLength];
                    Array.Copy(current.Types, updatedTypes, current.Length);

                    for (int i = current.Length; i < targetLength; i++)
                        updatedTypes[i] = getDefaultColumnTypeByte(keyMode, i);
                }

                updatedTypes[columnIndex] = newValue;
                applyColumnTypesAndPersist(keyMode, setting, updatedTypes);
            }
            catch (NotSupportedException)
            {
            }
        }

        public void SetColumnTypes(int keyMode, IReadOnlyList<EzColumnType> columnTypes)
        {
            ArgumentNullException.ThrowIfNull(columnTypes);

            try
            {
                var setting = getColumnTypeListSetting(keyMode);
                int targetLength = Math.Max(keyMode, columnTypes.Count);
                byte[] updatedTypes = new byte[targetLength];

                for (int i = 0; i < targetLength; i++)
                    updatedTypes[i] = i < columnTypes.Count ? (byte)columnTypes[i] : getDefaultColumnTypeByte(keyMode, i);

                KeyModeColumnData current = getOrBuildKeyModeColumnData(keyMode);
                if (areTypesEqual(current.Types, updatedTypes))
                    return;

                applyColumnTypesAndPersist(keyMode, setting, updatedTypes);
            }
            catch (NotSupportedException)
            {
            }
        }

        public void SetColumnType(int keyMode, int columnIndex, string colorType)
        {
            if (!tryParseColumnType(colorType, out var parsed))
                parsed = EzColumnTypeManager.GetColumnType(keyMode, columnIndex);

            SetColumnType(keyMode, columnIndex, parsed);
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
            // Hot path: read runtime compact data once and operate on locals only.
            double baseWidth = Get<double>(Ez2Setting.ColumnWidth);
            double specialFactor = Get<double>(Ez2Setting.SpecialFactor);

            int forMode = keyMode == 14 ? keyMode - 1 : keyMode;

            var data = getOrBuildKeyModeColumnData(keyMode);
            byte[] types = data.Types;
            ulong mask = data.SpecialMask;

            // Use double accumulator for precision then cast once.
            double total = 0.0;

            int upto = Math.Min(forMode, types.Length);
            int uptoMask = Math.Min(upto, 64);

            // Handle indices < 64 using mask (very fast bit ops).
            for (int i = 0; i < uptoMask; i++)
            {
                bool isSpecial = ((mask >> i) & 1UL) != 0UL;
                total += baseWidth * (isSpecial ? specialFactor : 1.0);
            }

            // Handle remaining indices (if any) by checking the types array.
            for (int i = uptoMask; i < upto; i++)
            {
                bool isSpecial = types[i] == (byte)EzColumnType.S;
                total += baseWidth * (isSpecial ? specialFactor : 1.0);
            }

            return (float)total;
        }

        public Colour4 GetColumnColor(int keyMode, int columnIndex)
        {
            EzColumnType colorType = GetColumnType(keyMode, columnIndex);
            return Get<Colour4>(getColorSetting(colorType));
        }

        public Bindable<Colour4> GetColumnColorBindable(int keyMode, int columnIndex)
        {
            EzColumnType colorType = GetColumnType(keyMode, columnIndex);
            return GetBindable<Colour4>(getColorSetting(colorType));
        }

        public bool IsSpecialColumn(int keyMode, int columnIndex)
        {
            return GetColumnType(keyMode, columnIndex) == EzColumnType.S;
        }

        // Fast accessors for hot paths: read runtime data only and avoid parsing/allocations.
        // These do NOT attempt to read settings from disk — they only consult already-built runtime data.
        public EzColumnType GetColumnTypeFast(int keyMode, int columnIndex)
        {
            if (runtime_column_data.TryGetValue(keyMode, out var data))
            {
                if (columnIndex < data.Length)
                    return (EzColumnType)data.Types[columnIndex];
            }

            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
        }

        public bool IsSpecialColumnFast(int keyMode, int columnIndex)
        {
            if (runtime_column_data.TryGetValue(keyMode, out var data))
            {
                if (columnIndex < data.Length)
                {
                    if (columnIndex < 64)
                    {
                        return ((data.SpecialMask >> columnIndex) & 1UL) != 0UL;
                    }

                    return data.Types[columnIndex] == (byte)EzColumnType.S;
                }
            }

            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex) == EzColumnType.S;
        }

        public bool[] GetSpecialColumnsBools(int keyMode)
        {
            KeyModeColumnData data = getOrBuildKeyModeColumnData(keyMode);

            bool[] result = new bool[data.Length];
            int uptoMask = Math.Min(data.Length, 64);

            for (int i = 0; i < uptoMask; i++)
                result[i] = ((data.SpecialMask >> i) & 1UL) != 0UL;

            for (int i = uptoMask; i < data.Length; i++)
                result[i] = data.Types[i] == (byte)EzColumnType.S;

            return result;
        }

        public EzColumnType GetColumnType(int keyMode, int columnIndex)
        {
            KeyModeColumnData data = getOrBuildKeyModeColumnData(keyMode);

            if (columnIndex < data.Length)
                return (EzColumnType)data.Types[columnIndex];

            return EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
        }

        public EzColumnType[] GetColumnTypes(int keyMode)
        {
            KeyModeColumnData data = getOrBuildKeyModeColumnData(keyMode);
            var arr = new EzColumnType[data.Length];

            for (int i = 0; i < data.Length; i++)
                arr[i] = (EzColumnType)data.Types[i];

            return arr;
        }

        // New helper: build runtime compact data from the stored setting string and populate public caches.
        private KeyModeColumnData buildKeyModeColumnDataFromSetting(int keyMode)
        {
            var setting = getColumnTypeListSetting(keyMode);
            string? columnColors = Get<string>(setting);

            byte[] typesBytes = new byte[keyMode];
            int parsedCount = 0;

            if (!string.IsNullOrEmpty(columnColors))
            {
                int start = 0;
                int index = 0;

                for (int i = 0; i <= columnColors.Length && index < keyMode; i++)
                {
                    if (i == columnColors.Length || columnColors[i] == ',')
                    {
                        string part = columnColors.Substring(start, i - start).Trim();
                        if (tryParseColumnType(part, out var t))
                            typesBytes[index] = (byte)t;
                        else
                            typesBytes[index] = getDefaultColumnTypeByte(keyMode, index);

                        index++;
                        start = i + 1;
                    }
                }

                parsedCount = index;
            }

            for (int i = parsedCount; i < keyMode; i++)
                typesBytes[i] = getDefaultColumnTypeByte(keyMode, i);

            var data = createRuntimeData(typesBytes);
            runtime_column_data[keyMode] = data;

            return data;
        }

        private KeyModeColumnData getOrBuildKeyModeColumnData(int keyMode)
        {
            if (runtime_column_data.TryGetValue(keyMode, out var data))
                return data;

            return buildKeyModeColumnDataFromSetting(keyMode);
        }

        private static KeyModeColumnData createRuntimeData(byte[] types)
        {
            ulong mask = 0;
            int upto = Math.Min(types.Length, 64);

            for (int i = 0; i < upto; i++)
            {
                if (types[i] == (byte)EzColumnType.S)
                    mask |= 1UL << i;
            }

            return new KeyModeColumnData(types, mask);
        }

        // Compatibility caches removed: runtime compact data is authoritative.

        private static Ez2Setting getColorSetting(EzColumnType colorType) => column_type_to_setting.TryGetValue(colorType, out var setting) ? setting : Ez2Setting.ColumnTypeA;

        private static byte getDefaultColumnTypeByte(int keyMode, int index) => (byte)EzColumnTypeManager.GetColumnType(keyMode, index);

        private static bool tryParseColumnType(string? raw, out EzColumnType columnType)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                columnType = default;
                return false;
            }

            return Enum.TryParse(raw.Trim(), out columnType);
        }

        private void applyColumnTypesAndPersist(int keyMode, Ez2Setting setting, byte[] updatedTypes)
        {
            var updatedData = createRuntimeData(updatedTypes);
            runtime_column_data[keyMode] = updatedData;

            string serialized = serializeColumnTypes(updatedTypes);
            if (!string.Equals(Get<string>(setting), serialized, StringComparison.Ordinal))
                SetValue(setting, serialized);
        }

        private static bool areTypesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static string serializeColumnTypes(byte[] types)
        {
            if (types.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(types.Length * 2);

            for (int i = 0; i < types.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');

                int typeIndex = types[i];
                if (typeIndex < column_type_names.Length)
                    builder.Append(column_type_names[typeIndex]);
                else
                    builder.Append(EzColumnTypeManager.GetColumnType(types.Length, i));
            }

            return builder.ToString();
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
            scheduleSave();
        }

        public new void Save()
        {
            base.Save();

            // If a save was pending, cancel the timer.
            lock (saveLock)
            {
                saveTimer?.Dispose();
                saveTimer = null;
            }
        }

        private void scheduleSave()
        {
            lock (saveLock)
            {
                // reset timer to fire after debounce period
                if (saveTimer == null)
                {
                    saveTimer = new System.Threading.Timer(_ =>
                    {
                        try
                        {
                            base.Save();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            lock (saveLock)
                            {
                                saveTimer?.Dispose();
                                saveTimer = null;
                            }
                        }
                    }, null, save_debounce_ms, System.Threading.Timeout.Infinite);
                }
                else
                {
                    saveTimer.Change(save_debounce_ms, System.Threading.Timeout.Infinite);
                }
            }
        }

        public void FlushSave()
        {
            lock (saveLock)
            {
                saveTimer?.Dispose();
                saveTimer = null;
            }

            base.Save();
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;

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
