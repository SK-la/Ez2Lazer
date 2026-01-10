// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public class EzManiaLocalizationManager : EzLocalizationManager
    {
        static EzManiaLocalizationManager()
        {
            // 使用反射为未设置英文的属性自动生成英文（属性名替换_为空格）
            var fields = typeof(EzManiaLocalizationManager).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(EzLocalisableString))
                {
                    if (field.GetValue(null) is EzLocalisableString instance && instance.English == null)
                    {
                        instance.English = field.Name.Replace("_", " ");
                    }
                }
            }
        }

        // 本地化字符串类，直接持有中文和英文
        public new class EzLocalisableString : EzLocalizationManager.EzLocalisableString
        {
            public EzLocalisableString(string chinese, string? english = null)
                : base(chinese, english) { }

            // 便捷构造函数：如果不提供英文，则稍后通过反射从属性名生成
            public EzLocalisableString(string chinese)
                : base(chinese) { }
        }

        // 公共属性定义本地化字符串，直接指定中文和英文
        public static readonly EzLocalisableString Mania_Specific_Key = new EzLocalisableString("Mania特定中文");
        // 添加更多属性...
    }
}
