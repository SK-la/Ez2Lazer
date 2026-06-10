// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    /// <summary>
    /// Immutable note preview parameters for a single comparison pane.
    /// </summary>
    public readonly struct EzSkinEditorNotePreviewRequest
    {
        public bool UseEzNoteVariants { get; init; }

        public EzSkinEditorNoteCompareKind CompareKind { get; init; }

        public RulesetInfo Ruleset { get; init; }

        public EzSkinEditorNotePart Part { get; init; }

        public string VariantId { get; init; }

        public Colour4 NoteColour { get; init; }

        public double Width { get; init; }

        public double Height { get; init; }

        public string ExportName { get; init; }
    }
}
