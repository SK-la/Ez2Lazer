// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public enum EzSkinEditorNoteCompareKind
    {
        [LocalisableDescription(typeof(EzEditorStrings), nameof(EzEditorStrings.NOTE_COMPARE_KIND_TAP))]
        Tap,

        [LocalisableDescription(typeof(EzEditorStrings), nameof(EzEditorStrings.NOTE_COMPARE_KIND_HOLD))]
        Hold,
    }
}
