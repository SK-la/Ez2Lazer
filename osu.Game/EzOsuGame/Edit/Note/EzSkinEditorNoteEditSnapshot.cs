// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    /// <summary>
    /// In-memory note edit comparison snapshot. Independent from <see cref="EzSkinEditorComparisonSnapshot"/>.
    /// </summary>
    public sealed class EzSkinEditorNoteEditSnapshot
    {
        public RulesetInfo Ruleset { get; private set; } = null!;

        public EzSkinEditorNotePart Part { get; private set; }

        public string VariantId { get; private set; } = string.Empty;

        public Colour4 NoteColour { get; private set; }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public string ExportName { get; private set; } = string.Empty;

        public void CaptureFrom(EzSkinEditorNoteEditSession session)
        {
            Ruleset = session.Ruleset.Value;
            Part = session.Part.Value;
            VariantId = session.VariantId.Value;
            NoteColour = session.NoteColour.Value;
            Width = session.Width.Value;
            Height = session.Height.Value;
            ExportName = session.ExportName.Value;
        }

        public void ApplyTo(EzSkinEditorNoteEditSession session)
        {
            session.Ruleset.Value = Ruleset;
            session.Part.Value = Part;
            session.VariantId.Value = VariantId;
            session.NoteColour.Value = NoteColour;
            session.Width.Value = Width;
            session.Height.Value = Height;
            session.ExportName.Value = ExportName;
        }

        public EzSkinEditorNotePreviewRequest ToPreviewRequest(bool useEzNoteVariants) => new EzSkinEditorNotePreviewRequest
        {
            UseEzNoteVariants = useEzNoteVariants,
            Ruleset = Ruleset,
            Part = Part,
            VariantId = VariantId,
            NoteColour = NoteColour,
            Width = Width,
            Height = Height,
            ExportName = ExportName,
        };
    }
}
