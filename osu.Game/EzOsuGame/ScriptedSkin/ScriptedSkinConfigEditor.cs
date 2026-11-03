// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 脚本皮肤配置编辑器，动态生成 [SkinConfig] 属性的 UI 控件。
    /// </summary>
    public partial class ScriptedSkinConfigEditor : Container
    {
        private IScriptedSkin? scriptedSkin;
        private FillFlowContainer? configControlsContainer;
        private readonly Dictionary<string, object?> pendingChanges = new Dictionary<string, object?>();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public ScriptedSkinConfigEditor()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        /// <summary>
        /// 设置要编辑的脚本皮肤实例。
        /// </summary>
        public void SetSkin(IScriptedSkin skin)
        {
            scriptedSkin = skin;
            BuildConfigUI();
        }

        /// <summary>
        /// 构建配置 UI，扫描所有 [SkinConfig] 属性并生成对应控件。
        /// </summary>
        public void BuildConfigUI()
        {
            Clear();

            if (scriptedSkin == null)
            {
                Add(createNoSkinMessage());
                return;
            }

            // 获取所有带 [SkinConfig] 标记的属性
            var configProperties = getConfigProperties();

            if (!configProperties.Any())
            {
                Add(createNoConfigMessage());
                return;
            }

            // 创建滚动容器
            var scrollContainer = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 400, // 固定高度，超出部分滚动
            };

            configControlsContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding(10),
            };

            // 为每个配置属性创建控件
            foreach (var property in configProperties)
            {
                var attribute = property.GetCustomAttribute<SkinConfigAttribute>();
                if (attribute == null)
                    continue;

                var metadata = ConfigMetadata.FromProperty(property, attribute);
                var control = ConfigControlFactory.CreateControl(metadata, value =>
                {
                    pendingChanges[metadata.PropertyName] = value;
                    applyPendingChanges();
                });

                configControlsContainer.Add(control);
            }

            scrollContainer.Child = configControlsContainer;
            Add(scrollContainer);
        }

        /// <summary>
        /// 应用待处理的更改到脚本皮肤。
        /// </summary>
        private void applyPendingChanges()
        {
            if (scriptedSkin == null)
                return;

            var type = scriptedSkin.GetType();

            foreach (var kvp in pendingChanges)
            {
                var property = type.GetProperty(kvp.Key);

                if (property != null && property.CanWrite)
                {
                    try
                    {
                        property.SetValue(scriptedSkin, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        Framework.Logging.Logger.Log($"Failed to set property '{kvp.Key}': {ex.Message}",
                            Framework.Logging.LoggingTarget.Runtime,
                            Framework.Logging.LogLevel.Error);
                    }
                }
            }

            // 清除已应用的更改
            pendingChanges.Clear();
        }

        /// <summary>
        /// 获取所有带 [SkinConfig] 标记的属性。
        /// </summary>
        private IEnumerable<PropertyInfo> getConfigProperties()
        {
            if (scriptedSkin == null)
                return Enumerable.Empty<PropertyInfo>();

            var type = scriptedSkin.GetType();
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .Where(p => p.GetCustomAttribute<SkinConfigAttribute>() != null);
        }

        private Container createNoSkinMessage()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 100,
                Child = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "No scripted skin loaded",
                    Font = OsuFont.Default.With(size: 16),
                    Colour = Color4.Gray,
                }
            };
        }

        private Container createNoConfigMessage()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 100,
                Child = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "No configurable properties found\nAdd [SkinConfig] attributes to your script",
                    Font = OsuFont.Default.With(size: 14),
                    Colour = Color4.Gray,
                }
            };
        }
    }
}
