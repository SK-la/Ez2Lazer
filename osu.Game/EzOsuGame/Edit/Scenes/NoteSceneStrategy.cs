// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class NoteSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzEditorSceneType SceneType => EzEditorSceneType.Note;

        public EzEditorSceneGroup Group => EzEditorSceneGroup.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.TAB_NOTE;

        public void ReleaseCachedDrawables() => sections = null;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new EzSkinEditorNoteRulesetSettingsSection(),
            new EzSkinEditorNoteEditSettingsSection(),
        };
    }
}
