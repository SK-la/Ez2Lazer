// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.LAsEzExtensions.Screens
{
    internal partial class EzEditorSidebar : EditorSidebar
    {
        public enum SidebarTab
        {
            Default,
            EzSettings,
            ColorSettings
        }

        private EzSkinSettings? ezSkinSettings;
        private SidebarTab currentTab = SidebarTab.Default;
        private Action<Container<EditorSidebarSection>>? lastPopulator;

        public EzEditorSidebar()
        {
            OsuTabControl<SidebarTab> tabControl;
            // 添加tabControl背景，防止内容遮挡tab标签
            var tabBackground = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Colour = Colour4.FromHex("222831") // 可根据主题调整
            };
            AddInternal(tabBackground);

            // 只添加tabControl，滚动和内容由基类EditorSidebar负责
            AddInternal(tabControl = new OsuTabControl<SidebarTab>
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Margin = new MarginPadding { Left = 5 },
                Items = new[] { SidebarTab.Default, SidebarTab.EzSettings, SidebarTab.ColorSettings }
            });
            //TODO 添加多列颜色选择
            // 设置内容区整体下移，避免与tab栏重叠
            Content.Margin = new MarginPadding { Top = 30 };

            tabControl.Current.ValueChanged += e =>
            {
                currentTab = e.NewValue;
                Content.Clear();

                switch (currentTab)
                {
                    case SidebarTab.EzSettings:
                        showEzSettings();
                        break;

                    case SidebarTab.ColorSettings:
                        showColorSettings();
                        break;

                    case SidebarTab.Default when lastPopulator != null:
                        PopulateSettings(lastPopulator);
                        break;
                }
            };
        }

        private void showEzSettings()
        {
            ezSkinSettings = new EzSkinSettings
            {
                RelativeSizeAxes = Axes.X
            };
            Content.Add(ezSkinSettings);
        }

        private void showColorSettings()
        {
            var ezColumnSettings = new EzColumnTab
            {
                RelativeSizeAxes = Axes.X
            };
            Content.Add(ezColumnSettings);
        }

        /// <summary>
        /// 仅在 Default tab 下允许填充内容。
        /// </summary>
        public void PopulateSettings(Action<Container<EditorSidebarSection>> populator)
        {
            lastPopulator = populator;
            if (currentTab != SidebarTab.Default)
                return;

            Content.Clear();
            populator(Content);
        }
    }
}
