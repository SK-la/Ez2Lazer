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
    /// 谱面编辑器组 · 配色：节拍线 palette（与 note 分母色同源键，仅影响 grid 绘制）。
    /// </summary>
    public class EditorColouringSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzEditorSceneGroup Group => EzEditorSceneGroup.BeatmapEditor;

        public EzEditorSceneType SceneType => EzEditorSceneType.EditorColouring;

        public LocalisableString TabTitle => EzEditorStrings.TAB_EDITOR_COLOURING;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new EzSkinEditorBeatSnapLineSettingsSection(),
        };
    }
}
