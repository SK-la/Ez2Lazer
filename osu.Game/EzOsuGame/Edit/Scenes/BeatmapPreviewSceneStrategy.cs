// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌组的谱面预览场景。协调器把真实预览屏插到屏幕栈，侧栏提供全谱预览绘制外观。
    /// </summary>
    public class BeatmapPreviewSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzSkinEditorSceneGroup Group => EzSkinEditorSceneGroup.SongSelect;

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.BeatmapPreview;

        public LocalisableString TabTitle => EzEditorStrings.TAB_BEATMAP_PREVIEW;

        public bool IsAvailable => true;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new BeatmapPreviewManiaSettingsSection(),
        };
    }
}
