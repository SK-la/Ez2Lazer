// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public interface IEzSkinEditorNoteComparisonSource
    {
        RulesetInfo? Ruleset { get; }

        EzSkinEditorNotePreviewRequest GetLiveRequest(EzSkinEditorNoteCompareKind compareKind);

        EzSkinEditorNotePreviewRequest GetSnapshotRequest(EzSkinEditorNoteCompareKind compareKind);
    }
}
