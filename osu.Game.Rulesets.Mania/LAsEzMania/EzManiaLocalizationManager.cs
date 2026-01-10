// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public class EzManiaLocalizationManager : EzLocalizationManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> maniaResources
            = new Dictionary<string, Dictionary<string, string>>();

        // 用于热重载的事件，当添加条目时触发
        public static event Action? OnResourcesChanged;

        static EzManiaLocalizationManager()
        {
            initializeManiaResources();
        }

        private static void initializeManiaResources()
        {
            // 添加Mania特定的本地化条目
            addManiaResource("Mania Specific Key", "Mania特定中文");
            // 添加更多条目...
        }

        private static void addManiaResource(string key, string chinese, string? english = null)
        {
            maniaResources[key] = new Dictionary<string, string>
            {
                ["zh"] = chinese,
                ["en"] = english ?? key
            };
            OnResourcesChanged?.Invoke(); // 触发热重载事件
        }

        public new static string GetString(string key)
        {
            // 先检查Mania自己的资源
            if (maniaResources.TryGetValue(key, out var maniaValue))
            {
                string lang = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", System.StringComparison.Ordinal) ? "zh" : "en";
                return maniaValue[lang];
            }

            // 然后检查基类的资源
            return EzLocalizationManager.GetString(key);
        }

        public new static string GetString(string key, params object[] args)
        {
            string format = GetString(key);
            return string.Format(format, args);
        }

        // 运行时添加条目的方法
        public static void AddResource(string key, string chinese, string? english = null)
        {
            addManiaResource(key, chinese, english);
        }
    }
}
