// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Screens
{
    [Cached]
    public class EzSkinSettingsManager : IniConfigManager<EzSkinSetting>, IGameplaySettings
    {
        private readonly Dictionary<string, string> columnColors = new Dictionary<string, string>();
        protected override string Filename => "EzSkinSettings.ini";

        protected override void InitialiseDefaults()
        {
            SetDefault(EzSkinSetting.NoteSetName, "evolve");
            SetDefault(EzSkinSetting.DynamicTracking, false);
            SetDefault(EzSkinSetting.GlobalTextureName, 4);
            SetDefault(EzSkinSetting.NonSquareNoteHeight, 25.0, 1.0, 100.0, 1.0);
            SetDefault(EzSkinSetting.VirtualHitPosition, 110.0, 0, 384.0, 1.0);

            SetDefault(EzSkinSetting.ColumnColorPrefix, string.Empty);
            SetDefault(EzSkinSetting.ColorSettingsEnabled, true);
            SetDefault(EzSkinSetting.SelectedKeyMode, 4);
            SetDefault(EzSkinSetting.AColorValue, Colour4.WhiteSmoke);
            SetDefault(EzSkinSetting.BColorValue, Colour4.CadetBlue);
            SetDefault(EzSkinSetting.Special1ColorValue, Colour4.Red);
            SetDefault(EzSkinSetting.Special2ColorValue, Colour4.Green);
        }

        public EzSkinSettingsManager(Storage storage)
            : base(storage)
        {
            // 加载时解析保存的列颜色设置
            string savedColors = Get<string>(EzSkinSetting.ColumnColorPrefix);

            if (!string.IsNullOrEmpty(savedColors))
            {
                foreach (string entry in savedColors.Split('|'))
                {
                    string[] parts = entry.Split('=');

                    if (parts.Length == 2)
                    {
                        columnColors[parts[0]] = parts[1];
                    }
                }
            }
        }

        public T Get<T>(string fullKey)
        {
            if (fullKey.StartsWith($"{EzSkinSetting.ColumnColorPrefix}:", StringComparison.Ordinal))
            {
                string colorKey = fullKey.Substring(fullKey.IndexOf(':') + 1);

                if (columnColors.TryGetValue(colorKey, out string? value))
                    return (T)(object)value;
            }

            if (typeof(T).IsClass)
            {
                // 尝试获取该类型的默认配置值
                // 此处可以根据具体类型添加更多处理逻辑
                return default!; // 保留原有行为，但应该在上面添加类型检查和处理
            }

            return default!;
        }

        public new Bindable<T> GetBindable<T>(EzSkinSetting setting)
        {
            // 从基类获取绑定对象（可能是 BindableNumber<T>）
            var bindable = base.GetBindable<T>(setting);
            // 由于子类可以安全转换为父类，这将正常工作
            return bindable;
        }

        public new void SetValue<T>(EzSkinSetting lookup, T value)
        {
            base.SetValue(lookup, value);
            NotifySettingsChanged();
        }

        public void SetValue<T>(string fullKey, T value)
        {
            if (fullKey.StartsWith($"{EzSkinSetting.ColumnColorPrefix}:", StringComparison.Ordinal))
            {
                string colorKey = fullKey.Substring(fullKey.IndexOf(':') + 1);
                columnColors[colorKey] = value?.ToString() ?? string.Empty;

                NotifySettingsChanged();
            }
        }

        public new void Save()
        {
            if (columnColors.Count > 0)
            {
                List<string> entries = new List<string>();

                foreach (var pair in columnColors)
                {
                    entries.Add($"{pair.Key}={pair.Value}");
                }

                SetValue(EzSkinSetting.ColumnColorPrefix, string.Join("|", entries));
            }

            base.Save();
            NotifySettingsChanged();
        }

        public event Action? OnSettingsChanged;

        protected void NotifySettingsChanged()
        {
            OnSettingsChanged?.Invoke();
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;
    }

    public enum EzSkinSetting
    {
        DynamicTracking,
        GlobalTextureName,
        NoteSetName,
        NonSquareNoteHeight,
        VirtualHitPosition,

        // 着色系统
        ColumnColorPrefix,
        ColorSettingsEnabled,
        SelectedKeyMode,
        AColorValue,
        BColorValue,
        Special1ColorValue,
        Special2ColorValue,
    }
}
