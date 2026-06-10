// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class EzSkinLNEditorProvider
    {
        public Drawable CreateNotePreview(ISkin skin, EzSkinEditorNotePreviewRequest request)
        {
            bool isHold = request.Part != EzSkinEditorNotePart.Note;
            string label = request.Part switch
            {
                EzSkinEditorNotePart.HoldHead => "LN Head",
                EzSkinEditorNotePart.HoldBody => "LN Body",
                EzSkinEditorNotePart.HoldTail => "LN Tail",
                _ => "Note",
            };

            var (columnIndex, isSpecial) = mapVariantToColumn(request.VariantId);

            return createPreviewRow(skin, label, isHold, columnIndex, isSpecial).With(d =>
            {
                d.RelativeSizeAxes = Axes.Both;
                d.Height = 0;
            });
        }

        private static (int columnIndex, bool isSpecial) mapVariantToColumn(string variantId) =>
            variantId switch
            {
                "S" or "P" => (0, true),
                "2" or "B" => (1, false),
                _ => (0, false),
            };
    }
}
