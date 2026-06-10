// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class NoteSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Note;

        public LocalisableString TabTitle => EzEditorStrings.TAB_NOTE;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorNoteComparisonHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            if (context.NoteSession == null)
                return Array.Empty<EzSkinEditorSidebarGroupDefinition>();

            return new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_NOTE_RULESET,
                    CreateContent = () => new EzSkinEditorNoteRulesetSettingsSection(
                        context.NoteSession,
                        () => context.RequestPreviewRefresh?.Invoke()),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_NOTE_EDIT,
                    CreateContent = () => new EzSkinEditorNoteEditSettingsSection(
                        context.NoteSession,
                        () => context.UsesEzNoteVariants,
                        () => context.CreateNoteSnapshot?.Invoke(),
                        () => context.RestoreNoteSnapshot?.Invoke(),
                        () => context.ExportNotePreview?.Invoke()),
                },
            };
        }
    }
}
