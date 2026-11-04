// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 谱面编辑器组 · 配色：网格预览、各规则集配色栏目、节拍线 palette。
    /// </summary>
    public class EditorColouringSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;
        private Ruleset[] rulesets = [];

        public EzEditorSceneGroup Group => EzEditorSceneGroup.BeatmapEditor;

        public EzEditorSceneType SceneType => EzEditorSceneType.EditorColouring;

        public LocalisableString TabTitle => EzEditorStrings.TAB_EDITOR_COLOURING;

        public void AttachRulesets(IReadOnlyList<Ruleset> rulesets)
        {
            if (sections != null)
                return;

            this.rulesets = rulesets.ToArray();
        }

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= createSections();

        private EzSkinEditorSettingsGroup[] createSections()
        {
            var result = new List<EzSkinEditorSettingsGroup>
            {
                new EzSkinEditorComposeGridPreviewSettingsSection(),
            };

            foreach (var ruleset in rulesets)
                result.AddRange(ruleset.CreateSkinEditorColouringSections());

            result.Add(new EzSkinEditorBeatSnapLineSettingsSection());
            return result.ToArray();
        }
    }
}
