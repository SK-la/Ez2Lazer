// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Shared dependencies passed into scene strategies when building content and sidebar groups.
    /// </summary>
    public sealed class EzSkinEditorSceneContext
    {
        public ISkinEditorVirtualProvider? Provider { get; init; }

        public ISkin EditorSkin { get; init; } = null!;

        /// <summary>
        /// Equipped skin instance (not the editor preview wrapper). Used for variant family detection.
        /// </summary>
        public ISkin ActualSkin { get; init; } = null!;

        public bool UsesEzNoteVariants { get; init; }

        public EzSkinIniSession? SkinIniSession { get; init; }

        public EzSkinJsonSession? SkinJsonSession { get; init; }

        public EzSkinEditorPreviewState? PreviewState { get; init; }

        public Action? RequestSceneRefresh { get; init; }

        /// <summary>
        /// Rebuild scene content only (preview area), without replacing sidebar groups.
        /// </summary>
        public Action? RequestPreviewRefresh { get; init; }

        public Action? CommitSkinIni { get; init; }

        public EzSkinEditorPreviewSource PreviewSource { get; init; } = EzSkinEditorPreviewSource.Static;

        /// <summary>
        /// Beatmap preview is only shown in the appearance scene.
        /// </summary>
        public bool AllowBeatmapPreview { get; init; }

        /// <summary>
        /// Size/colour scenes use the virtual playfield on the left and Note/LN comparison on the right.
        /// </summary>
        public bool UseVirtualComparisonPreview { get; init; }

        /// <summary>
        /// Note scene uses full-width live vs note-edit-snapshot comparison only.
        /// </summary>
        public bool UseNoteComparisonOnly { get; init; }

        public EzSkinEditorNoteEditSession? NoteSession { get; init; }

        public EzSkinEditorNoteEditSnapshot? NoteSnapshot { get; init; }

        public Action? CreateNoteSnapshot { get; init; }

        public Action? RestoreNoteSnapshot { get; init; }

        public Action? ExportNotePreview { get; init; }

        public IWorkingBeatmap? PreviewBeatmap { get; init; }

        public RulesetInfo? PreviewRuleset { get; init; }

        public EzBeatmapPreviewMode PreviewMode { get; init; } = EzBeatmapPreviewMode.Static;

        /// <summary>
        /// Configuration snapshot used for comparison preview (right pane) and control reset baselines.
        /// Covers Ez2Config and skin.ini only — not RulesetConfig, ScriptedSkin, or Note edit state.
        /// </summary>
        public EzSkinEditorComparisonSnapshot? ComparisonSnapshot { get; init; }
    }
}
