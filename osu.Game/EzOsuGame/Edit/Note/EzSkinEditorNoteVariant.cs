// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit.Note
{
    public readonly struct EzSkinEditorNoteVariant
    {
        public string Id { get; init; }

        public string Label { get; init; }

        public EzSkinEditorNoteVariant(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }
}
