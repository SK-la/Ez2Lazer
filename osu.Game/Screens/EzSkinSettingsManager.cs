// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Screens.LAsEzExtensions;

namespace osu.Game.Screens
{
    [Cached]
    public class EzSkinSettingsManager : IniConfigManager<EzSkinSetting>, IGameplaySettings
    {
        protected override string Filename => "EzSkinSettings.ini";
        private readonly int[] commonKeyModes = { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        protected override void InitialiseDefaults()
        {
            SetDefault(EzSkinSetting.SelectedKeyMode, 4);

            SetDefault(EzSkinSetting.ColumnWidth, 60, 5, 400.0, 1.0);
            SetDefault(EzSkinSetting.SpecialFactor, 1.2, 0.5, 2.0, 0.1);
            SetDefault(EzSkinSetting.HitPosition, 110.0, 0, 500, 1.0);
            SetDefault(EzSkinSetting.VisualHitPosition, 0.0, -100, 100, 1.0);

            SetDefault(EzSkinSetting.DynamicTracking, false);
            SetDefault(EzSkinSetting.GlobalTextureName, 4);

            SetDefault(EzSkinSetting.NoteSetName, "evolve");
            SetDefault(EzSkinSetting.StageName, "JIYU");
            SetDefault(EzSkinSetting.NonSquareNoteHeight, 28.0, 1.0, 100.0, 1.0);
            SetDefault(EzSkinSetting.NoteTrackLine, true);
            SetDefault(EzSkinSetting.NoteTrackLineHeight, 300, 0, 1000, 5.0);

            SetDefault(EzSkinSetting.ColorSettingsEnabled, true);
            SetDefault(EzSkinSetting.ColorA, Colour4.FromHex("#F5F5F5"));
            SetDefault(EzSkinSetting.ColorB, Colour4.FromHex("#648FFF"));
            SetDefault(EzSkinSetting.ColorS1, Colour4.FromHex("#FF4A4A"));
            SetDefault(EzSkinSetting.ColorS2, Colour4.FromHex("#72FF72"));

            foreach (int keyMode in commonKeyModes)
            {
                EzSkinSetting setting = getColumnColorSetting(keyMode);
                SetDefault(setting, string.Join(",", getDefaultColumnTypes(keyMode)));
            }
        }

        private EzSkinSetting getColumnColorSetting(int keyMode)
        {
            return keyMode switch
            {
                4 => EzSkinSetting.ColumnColor4K,
                5 => EzSkinSetting.ColumnColor5K,
                6 => EzSkinSetting.ColumnColor6K,
                7 => EzSkinSetting.ColumnColor7K,
                8 => EzSkinSetting.ColumnColor8K,
                9 => EzSkinSetting.ColumnColor9K,
                10 => EzSkinSetting.ColumnColor10K,
                12 => EzSkinSetting.ColumnColor12K,
                14 => EzSkinSetting.ColumnColor14K,
                16 => EzSkinSetting.ColumnColor16K,
                18 => EzSkinSetting.ColumnColor18K,
                _ => throw new NotSupportedException($"不支持 {keyMode} 键位模式")
            };
        }

        private string[] getDefaultColumnTypes(int keyMode)
        {
            string[] types = new string[keyMode];

            for (int i = 0; i < keyMode; i++)
            {
                types[i] = EzColumnTypeManager.GetColumnColorType(keyMode, i);
            }

            return types;
        }

        public EzSkinSettingsManager(Storage storage)
            : base(storage)
        {
            initializeEvents();
        }

        public string GetColumnType(int keyMode, int columnIndex)
        {
            try
            {
                EzSkinSetting setting = getColumnColorSetting(keyMode);
                string columnColors = Get<string>(setting);

                string[] types = columnColors.Split(',');

                if (columnIndex < types.Length && !string.IsNullOrEmpty(types[columnIndex]))
                    return types[columnIndex];

                return EzColumnTypeManager.GetColumnColorType(keyMode, columnIndex);
            }
            catch (NotSupportedException)
            {
                return EzColumnTypeManager.GetColumnColorType(keyMode, columnIndex);
            }
        }

        public void SetColumnType(int keyMode, int columnIndex, string colorType)
        {
            try
            {
                EzSkinSetting setting = getColumnColorSetting(keyMode);
                string currentConfig = Get<string>(setting);
                string[] types = !string.IsNullOrEmpty(currentConfig)
                    ? currentConfig.Split(',')
                    : new string[keyMode];

                if (types.Length <= columnIndex)
                {
                    Array.Resize(ref types, Math.Max(keyMode, columnIndex + 1));
                }

                types[columnIndex] = colorType.Trim();
                SetValue(setting, string.Join(",", types));
            }
            catch (NotSupportedException)
            {
            }
        }

        public Colour4 GetColumnColor(int keyMode, int columnIndex)
        {
            string colorType = GetColumnType(keyMode, columnIndex);

            EzSkinSetting setting = colorType switch
            {
                "S1" => EzSkinSetting.ColorS1,
                "S2" => EzSkinSetting.ColorS2,
                "B" => EzSkinSetting.ColorB,
                _ => EzSkinSetting.ColorA
            };

            return Get<Colour4>(setting);
        }

        public new Bindable<T> GetBindable<T>(EzSkinSetting setting)
        {
            return base.GetBindable<T>(setting);
        }

        public event Action? OnSettingsChanged;
        public event Action? OnColumnChanged;

        private void initializeEvents()
        {
            GetBindable<double>(EzSkinSetting.ColumnWidth).ValueChanged += e => OnColumnChanged?.Invoke();
            GetBindable<double>(EzSkinSetting.SpecialFactor).ValueChanged += e => OnColumnChanged?.Invoke();

            GetBindable<double>(EzSkinSetting.HitPosition).ValueChanged += e => OnSettingsChanged?.Invoke();
            GetBindable<double>(EzSkinSetting.VisualHitPosition).ValueChanged += e => OnSettingsChanged?.Invoke();

            GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).ValueChanged += e => OnSettingsChanged?.Invoke();
            GetBindable<double>(EzSkinSetting.NoteTrackLineHeight).ValueChanged += e => OnSettingsChanged?.Invoke();
        }

        protected void NotifySettingsChanged()
        {
            OnSettingsChanged?.Invoke();
            OnColumnChanged?.Invoke();
        }

        public new void SetValue<T>(EzSkinSetting lookup, T value)
        {
            base.SetValue(lookup, value);
            NotifySettingsChanged();
        }

        public new void Save()
        {
            base.Save();
            NotifySettingsChanged();
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;
    }

    public enum EzSkinSetting
    {
        SelectedKeyMode,

        // 轨道相关
        ColumnWidth,
        SpecialFactor,
        HitPosition,
        VisualHitPosition,
        NonSquareNoteHeight,
        NoteTrackLine,
        NoteTrackLineHeight,

        // 皮肤设置
        DynamicTracking,
        GlobalTextureName,
        NoteSetName,
        StageName,

        // 着色系统
        ColorSettingsEnabled,
        ColorA,
        ColorB,
        ColorS1,
        ColorS2,

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
    }
}

// private void setDefaultColumnColor()
// {
//     foreach (int keyMode in commonKeyModes)
//     {
//         EzSkinSetting setting = getColumnColorSetting(keyMode);
//
//         string existingConfig = Get<string>(setting);
//
//         if (string.IsNullOrEmpty(existingConfig))
//         {
//             string[] types = new string[keyMode];
//
//             for (int i = 0; i < keyMode; i++)
//             {
//                 types[i] = EzColumnTypeManager.GetColumnColorType(keyMode, i);
//             }
//
//             SetValue(setting, string.Join(",", types));
//         }
//     }
// }
