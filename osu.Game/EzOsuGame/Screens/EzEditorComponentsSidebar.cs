// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.EzOsuGame.Screens
{
    internal partial class EzEditorComponentsSidebar : EditorSidebar
    {
        public enum SidebarTab
        {
            Select = 0,
            EzSkins = 1,
        }

        public event Action<SidebarTab>? TabChanged;
        private SidebarTab currentTab = SidebarTab.Select;
        private readonly List<EditorSidebarSection> selectSections = new List<EditorSidebarSection>();

        public EzEditorComponentsSidebar()
        {
            OsuTabControl<SidebarTab> tabControl1;
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Colour = Colour4.FromHex("222831")
            });

            AddInternal(tabControl1 = new OsuTabControl<SidebarTab>
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Margin = new MarginPadding { Left = 5 },
                Items = new[] { SidebarTab.Select, SidebarTab.EzSkins }
            });

            Content.Margin = new MarginPadding { Top = 30 };

            tabControl1.Current.ValueChanged += e =>
            {
                currentTab = e.NewValue;
                applyCurrentTabView();
                TabChanged?.Invoke(e.NewValue);
            };
        }

        public void SetSelectSections(IEnumerable<EditorSidebarSection> sections)
        {
            selectSections.Clear();
            selectSections.AddRange(sections);
            applyCurrentTabView();
        }

        private void applyCurrentTabView()
        {
            // 清理后不释放资源，防止切换tab报错
            Content.Clear(false);

            if (currentTab == SidebarTab.EzSkins)
            {
                Content.Add(new EzEditorSectionComponentsSidebar
                {
                    RelativeSizeAxes = Axes.X
                });
                return;
            }

            if (selectSections.Count > 0)
                Content.AddRange(selectSections);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // 清理保存的 section 引用，避免内存泄漏
            selectSections.Clear();
        }
    }
}
