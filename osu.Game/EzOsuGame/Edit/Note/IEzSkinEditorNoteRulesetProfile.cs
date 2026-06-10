// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public interface IEzSkinEditorNoteRulesetProfile
    {
        int RulesetOnlineId { get; }

        RulesetInfo RulesetInfo { get; }

        IReadOnlyList<EzSkinEditorNotePart> SupportedParts { get; }

        IReadOnlyList<EzSkinEditorNoteVariant> GetVariants(bool useEzNoteVariants, EzSkinEditorNotePart part);

        string GetDefaultVariantId(bool useEzNoteVariants, EzSkinEditorNotePart part);

        string? ResolveTextureName(bool useEzNoteVariants, EzSkinEditorNotePart part, string variantId);

        Drawable? CreateRulesetSettingsContent();
    }
}
