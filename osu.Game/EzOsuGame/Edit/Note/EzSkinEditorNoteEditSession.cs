// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    /// <summary>
    /// In-memory note edit state for the Note scene. Not persisted to Ez2Config or skin.ini.
    /// </summary>
    public sealed class EzSkinEditorNoteEditSession
    {
        public Bindable<RulesetInfo> Ruleset { get; } = new Bindable<RulesetInfo>();

        public Bindable<EzSkinEditorNotePart> Part { get; } = new Bindable<EzSkinEditorNotePart>(EzSkinEditorNotePart.Note);

        public Bindable<string> VariantId { get; } = new Bindable<string>(string.Empty);

        public Bindable<Colour4> NoteColour { get; } = new Bindable<Colour4>(Colour4.White);

        public Bindable<bool> TrueColouring { get; } = new Bindable<bool>();

        public BindableNumber<double> Width { get; } = new BindableDouble(100) { MinValue = 1, MaxValue = 512 };

        public BindableNumber<double> Height { get; } = new BindableDouble(100) { MinValue = 1, MaxValue = 512 };

        public Bindable<string> ExportName { get; } = new Bindable<string>("note-preview");

        public EzSkinEditorNotePreviewRequest ToPreviewRequest(bool useEzNoteVariants, EzSkinEditorNoteCompareKind compareKind) => new EzSkinEditorNotePreviewRequest
        {
            UseEzNoteVariants = useEzNoteVariants,
            CompareKind = compareKind,
            Ruleset = Ruleset.Value,
            Part = Part.Value,
            VariantId = VariantId.Value,
            NoteColour = NoteColour.Value,
            Width = Width.Value,
            Height = Height.Value,
            ExportName = ExportName.Value,
        };
    }
}
