// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public interface IEzSkinEditorNoteRulesetProfile
    {
        int RulesetOnlineId { get; }

        RulesetInfo RulesetInfo { get; }

        IReadOnlyList<EzSkinEditorNotePart> SupportedParts { get; }

        IReadOnlyList<EzSkinEditorNoteVariant> GetVariants(ISkin skin, EzSkinEditorNotePart part);

        string GetDefaultVariantId(ISkin skin, EzSkinEditorNotePart part);

        Drawable CreateNotePreview(ISkin skin, EzSkinEditorNotePreviewRequest request);

        Drawable CreateRulesetSettingsContent();
    }
}
