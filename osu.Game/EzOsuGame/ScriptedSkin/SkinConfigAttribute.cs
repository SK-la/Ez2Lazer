// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 标记可配置的属性，会在 EzSkinEditorScreen 中自动生成对应的 UI 控件。
    /// </summary>
    /// <remarks>
    /// 使用此属性标记脚本皮肤中的公共属性，编辑器会自动识别并生成相应的配置界面。
    /// 支持的类型包括：float（滑块）、int（整数滑块）、bool（开关）、Color4（颜色选择器）、string（文本输入）。
    /// </remarks>
    /// <example>
    /// <code>
    /// [SkinConfig("Note Scale", min: 0.5f, max: 2.0f, default: 1.0f, description: "调整 Note 的大小")]
    /// public float NoteScale { get; set; } = 1.0f;
    ///
    /// [SkinConfig("Combo Color", default: "#FF6B6B", description: "连击时的颜色")]
    /// public Color4 ComboColor { get; set; } = new Color4(1.0f, 0.42f, 0.42f, 1.0f);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class SkinConfigAttribute : Attribute
    {
        /// <summary>
        /// 在 UI 中显示的友好名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 默认值。
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// 最小值（仅适用于数值类型）。
        /// </summary>
        public object? Min { get; set; }

        /// <summary>
        /// 最大值（仅适用于数值类型）。
        /// </summary>
        public object? Max { get; set; }

        /// <summary>
        /// 属性的描述信息，会在鼠标悬浮时显示。
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 创建配置属性标记。
        /// </summary>
        /// <param name="displayName">显示名称。</param>
        public SkinConfigAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// 配置项的元数据，用于在运行时动态生成 UI。
    /// </summary>
    public class ConfigMetadata
    {
        /// <summary>
        /// 属性名称（代码中使用）。
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（UI 中显示）。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 属性类型。
        /// </summary>
        public Type PropertyType { get; set; } = typeof(object);

        /// <summary>
        /// 默认值。
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// 最小值。
        /// </summary>
        public object? Min { get; set; }

        /// <summary>
        /// 最大值。
        /// </summary>
        public object? Max { get; set; }

        /// <summary>
        /// 描述信息。
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 从属性和标记中提取元数据。
        /// </summary>
        /// <param name="property">属性信息。</param>
        /// <param name="attribute">配置标记。</param>
        /// <returns>配置元数据实例。</returns>
        public static ConfigMetadata FromProperty(PropertyInfo property, SkinConfigAttribute attribute)
        {
            return new ConfigMetadata
            {
                PropertyName = property.Name,
                DisplayName = attribute.DisplayName,
                PropertyType = property.PropertyType,
                DefaultValue = attribute.DefaultValue,
                Min = attribute.Min,
                Max = attribute.Max,
                Description = attribute.Description,
            };
        }
    }
}
