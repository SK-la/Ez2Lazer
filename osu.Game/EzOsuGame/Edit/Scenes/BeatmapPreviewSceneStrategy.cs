// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Screens;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌组的谱面预览场景。协调器把真实 <see cref="EzSkinEditorPreviewManiaScreen"/> 插到屏幕栈，
    /// 编辑器用 SetCustomRect 给空间；侧栏提供全谱预览绘制外观（note 部件颜色 + 厚度）。
    /// </summary>
    public class BeatmapPreviewSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public const string GROUP_ID = "BeatmapPreview";
        public EzSkinEditorSceneGroup Group => EzSkinEditorSceneGroup.SongSelect;

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.BeatmapPreview;

        public LocalisableString TabTitle => EzEditorStrings.TAB_BEATMAP_PREVIEW;

        public bool IsAvailable => true;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            if (context.BeatmapPreviewManiaSettings == null)
                return System.Array.Empty<EzSkinEditorSidebarGroupDefinition>();

            return new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID,
                    Title = EzEditorStrings.GROUP_BEATMAP_PREVIEW_MANIA,
                    CreateContent = () => new BeatmapPreviewManiaSettingsSection(context.BeatmapPreviewManiaSettings),
                },
            };
        }
    }
}
