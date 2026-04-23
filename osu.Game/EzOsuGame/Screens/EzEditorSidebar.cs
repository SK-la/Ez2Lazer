// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.EzOsuGame.Screens
{
    internal partial class EzEditorSidebar : EditorSidebar
    {
        public enum SidebarTab
        {
            Select = 0,

            EzSkin = 1,

            Column = 2
        }

        private EzSkinTab? ezSkinSettings;
        private SidebarTab currentTab = SidebarTab.Select;
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
                Items = new[] { SidebarTab.Select, SidebarTab.EzSkin, SidebarTab.Column }
            });

            // 设置内容区整体下移，避免与tab栏重叠
            Content.Margin = new MarginPadding { Top = 30 };

            tabControl.Current.ValueChanged += e =>
            {
                currentTab = e.NewValue;
                Content.Clear();

                switch (currentTab)
                {
                    case SidebarTab.EzSkin:
                        showEzSettings();
                        break;

                    case SidebarTab.Column:
                        showColumnSettings();
                        break;

                    case SidebarTab.Select when lastPopulator != null:
                        PopulateSettings(lastPopulator);
                        break;
                }
            };
        }

        private void showEzSettings()
        {
            ezSkinSettings = new EzSkinTab
            {
                RelativeSizeAxes = Axes.X
            };
            Content.Add(ezSkinSettings);
        }

        private void showColumnSettings()
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
            if (currentTab != SidebarTab.Select)
                return;

            Content.Clear();
            populator(Content);
        }
    }
}
