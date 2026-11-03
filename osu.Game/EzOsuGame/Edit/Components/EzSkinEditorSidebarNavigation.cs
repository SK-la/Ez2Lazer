// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 侧栏「导航轨选中项 ↔ 设置面板滚动」的双向同步。轨上的项目、分组可见性、每个场景
    /// 记忆的选中项，以及防止两侧互相回写的重入状态都由这里持有；侧栏只负责把分组
    /// Drawable 建出来并交给这里定位。
    /// </summary>
    public sealed class EzSkinEditorSidebarNavigation
    {
        // 视口顶判定为「已进入下一分组」的像素容差。
        private const double scroll_position_epsilon = 1;

        private readonly EzSkinEditorSidebarGroupRail rail;
        private readonly OsuScrollContainer scroll;
        private readonly IReadOnlyList<EzSkinEditorSettingsGroup> groups;

        private readonly Dictionary<EzSkinEditorSceneType, int> rememberedIndex = new Dictionary<EzSkinEditorSceneType, int>();

        private EzSkinEditorSceneType? scene;

        // 滚动驱动的选中变化期间置位，阻止选择回调再把视口拉回分组顶部。
        private bool suppressScrollResponse;

        // 分组重建后先等一帧让新分组完成布局，期间不反推选中项。
        private bool syncArmed;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Pages { get; private set; } = Array.Empty<EzSkinEditorSettingsGroup>();

        public int SelectedIndex => rail.SelectedIndex.Value;

        public EzSkinEditorSidebarNavigation(EzSkinEditorSidebarGroupRail rail, OsuScrollContainer scroll, IReadOnlyList<EzSkinEditorSettingsGroup> groups)
        {
            this.rail = rail;
            this.scroll = scroll;
            this.groups = groups;

            rail.SelectedIndex.BindValueChanged(onSelectionChanged);
        }

        /// <summary>
        /// 换上这一场景的栏目。栏目对象由场景持有，这里只更新导航轨。
        /// </summary>
        public void ShowSections(EzSkinEditorSceneType nextScene, IReadOnlyList<EzSkinEditorSettingsGroup> sections)
        {
            scene = nextScene;
            Pages = sections;

            rail.SetItems(sections.Select(s => (s.SectionTitle, s.SectionVisible.Value)).ToList());

            if (scene is EzSkinEditorSceneType currentScene
                && rememberedIndex.TryGetValue(currentScene, out int remembered)
                && remembered >= 0 && remembered < sections.Count
                && sections[remembered].SectionVisible.Value)
            {
                rail.SelectedIndex.Value = remembered;
            }
            else
            {
                int first = sections.ToList().FindIndex(s => s.SectionVisible.Value);
                rail.SelectedIndex.Value = first >= 0 ? first : 0;
            }
        }

        public void SetSectionVisible(int index, bool visible) => rail.SetItemVisible(index, visible);

        /// <summary>
        /// 换用新的分组定义，但不重建己有的分组 Drawable。
        /// </summary>
        public void SelectPage(int index)
        {
            if (index < 0 || index >= Pages.Count)
                return;

            rail.SelectedIndex.Value = index;
        }

        /// <summary>
        /// 分组重建期间暂停反推选中项，直到 <see cref="ResumeSync"/>。
        /// </summary>
        public void SuspendSync() => syncArmed = false;

        /// <summary>
        /// 重建后一帧恢复：先把视口对到当前选中项，再允许滚动反推。
        /// </summary>
        public void ResumeSync()
        {
            scrollToGroup(SelectedIndex);
            syncArmed = true;
        }

        /// <summary>
        /// 视口顶所在分组回写为选中项；用户滚动设置面板时导航高亮跟随。
        /// 以 Target 而非 Current 为基准：滚轮一动即切换，且不会与缓动中的位置来回抖动。
        /// </summary>
        public void SyncSelectionToScroll()
        {
            if (!syncArmed)
                return;

            int index = groupAtScrollTarget();

            if (index < 0 || index == rail.SelectedIndex.Value)
                return;

            suppressScrollResponse = true;

            try
            {
                rail.SelectedIndex.Value = index;
            }
            finally
            {
                suppressScrollResponse = false;
            }
        }

        private void onSelectionChanged(ValueChangedEvent<int> change)
        {
            if (scene is EzSkinEditorSceneType currentScene)
                rememberedIndex[currentScene] = change.NewValue;

            scrollToGroup(change.NewValue);
        }

        private void scrollToGroup(int index)
        {
            if (suppressScrollResponse)
                return;

            if (index < 0 || index >= groups.Count || !groups[index].SectionVisible.Value)
                return;

            scroll.ScrollTo(groups[index]);
        }

        private int groupAtScrollTarget()
        {
            int firstVisible = groupToVisibleIndex(0);
            if (firstVisible < 0)
                return -1;

            // 贴底时末尾分组可能整块落在视口内却拿不到视口顶，取最后一组，否则永远选不中。
            // 但内容未超出视口（无可滚动余量）时视口顶恒为列表顶，不能判成末组。
            double target = scroll.Target;
            double scrollableExtent = scroll.ScrollableExtent;

            if (scrollableExtent > 0 && target >= scrollableExtent - Precision.FLOAT_EPSILON)
                return groupToVisibleIndex(groups.Count - 1);

            for (int i = 0; i < groups.Count; i++)
            {
                if (!groups[i].SectionVisible.Value)
                    continue;

                var group = groups[i];

                // Pin 复选框等非分组内容位于列表顶部，视口顶尚未进入首个分组时挂到它上面。
                if (scroll.GetChildPosInContent(group) > target + scroll_position_epsilon)
                    return i > 0 ? groupToVisibleIndex(i - 1) : firstVisible;
            }

            return groupToVisibleIndex(groups.Count - 1);
        }

        /// <summary>
        /// 返回从 <paramref name="from"/> 起向前（含自身）最近一个可见分组下标；全不可见时返回第一个可见分组，没有则 -1。
        /// </summary>
        private int groupToVisibleIndex(int from)
        {
            for (int i = Math.Min(from, groups.Count - 1); i >= 0; i--)
            {
                if (groups[i].SectionVisible.Value)
                    return i;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].SectionVisible.Value)
                    return i;
            }

            return -1;
        }
    }
}
