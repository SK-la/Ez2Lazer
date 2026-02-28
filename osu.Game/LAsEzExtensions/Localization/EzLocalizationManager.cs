// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Reflection;
using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Localization
{
    public class EzLocalizationManager
    {
        /// <summary>
        /// Auto-fills missing English values for <see cref="EzLocalisableString"/> fields.
        /// Call this in a strings class static constructor when you rely on auto-generated English.
        /// </summary>
        /// <param name="type">The strings class type containing public static fields.</param>
        /// <example>
        /// <code>
        /// public static class MyStrings
        /// {
        ///     static MyStrings() => EzLocalizationManager.AutoFillEnglish(typeof(MyStrings));
        ///     public static readonly LocalisableString My_Button = new("我的按钮");
        /// }
        /// </code>
        /// </example>
        internal static void AutoFillEnglish(Type type)
        {
            // 使用反射为未设置英文的属性自动生成英文 (属性名替换_为空格)
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(EzLocalisableString))
                {
                    if (field.GetValue(null) is EzLocalisableString instance && instance.English == null)
                        instance.English = field.Name.Replace("_", " ");
                }
            }
        }

        /// <summary>
        /// 本地化字符串类, 直接持有中文和英文文本。
        /// 支持隐式转换为字符串, 根据当前 UI 文化自动返回相应语言的文本。
        /// </summary>
        /// <example>
        /// 便捷用法：如果不提供英文参数, 系统会自动从属性名生成英文 (将 '_' 替换为空格) 。
        /// <code>
        /// public static readonly EzLocalisableString My_Button = new EzLocalisableString("我的按钮");
        /// // 中文: "我的按钮"
        /// // 英文: "My Button" (自动生成)
        /// </code>
        /// 手动提供英文：
        /// <code>
        /// public static readonly EzLocalisableString My_Button = new EzLocalisableString("我的按钮", "Custom English");
        /// </code>
        /// </example>
        public class EzLocalisableString : ILocalisableStringData
        {
            public string Chinese { get; }

            /// <summary>
            /// 英文文本。如果为 null, 英文时显示中文, 或通过反射自动生成。
            /// </summary>
            public string? English { get; set; }

            /// <summary>
            /// 初始化本地化字符串。
            /// </summary>
            /// <param name="chinese">中文文本。</param>
            /// <param name="english">英文文本。如果为 null, 英文时显示中文, 或自动生成。</param>
            public EzLocalisableString(string chinese, string? english = null)
            {
                Chinese = chinese;
                English = english;
            }

            /// <summary>
            /// 隐式转换为字符串, 根据当前语言返回相应文本。
            /// </summary>
            /// <param name="s">本地化字符串实例。</param>
            /// <returns>当前语言的文本。</returns>
            public static implicit operator string(EzLocalisableString s) => s.getString();

            /// <summary>
            /// 隐式转换为 LocalisableString, 用于与 osu.Framework 兼容。
            /// </summary>
            /// <param name="s">本地化字符串实例。</param>
            /// <returns>LocalisableString 实例。</returns>
            public static implicit operator LocalisableString(EzLocalisableString s) => new LocalisableString((ILocalisableStringData)s);

            /// <summary>
            /// 支持格式化, 返回格式化后的字符串。
            /// </summary>
            /// <param name="args">格式化参数。</param>
            /// <returns>格式化后的文本。</returns>
            public string Format(params object[] args) => string.Format(getString(), args);

            /// <summary>
            /// 返回当前语言的文本。
            /// </summary>
            /// <returns>文本字符串。</returns>
            public override string ToString() => getString();

            private string getString()
            {
                string lang = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.Ordinal) ? "zh" : "en";
                return lang == "zh" ? Chinese : (English ?? Chinese);
            }

            /// <summary>
            /// 实现ILocalisableStringData接口的方法。
            /// </summary>
            public string GetLocalised(LocalisationParameters parameters) => getString();

            /// <summary>
            /// 实现ILocalisableStringData接口的Equals方法。
            /// </summary>
            public bool Equals(ILocalisableStringData? other)
            {
                if (other is not EzLocalisableString ezOther)
                    return false;

                return Chinese == ezOther.Chinese && English == ezOther.English;
            }
        }
    }
}
