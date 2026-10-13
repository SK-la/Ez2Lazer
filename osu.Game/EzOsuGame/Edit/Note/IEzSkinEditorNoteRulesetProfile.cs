// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public interface IEzSkinEditorNoteRulesetProfile
    {
        int RulesetOnlineId { get; }

        RulesetInfo RulesetInfo { get; }

        IReadOnlyList<EzManiaNotePart> SupportedParts { get; }

        IReadOnlyList<EzSkinEditorNoteVariant> GetVariants(bool useEzNoteVariants, EzManiaNotePart part);

        string GetDefaultVariantId(bool useEzNoteVariants, EzManiaNotePart part);

        string? ResolveTextureName(bool useEzNoteVariants, EzManiaNotePart part, string variantId);

        Texture? GetNoteTexture(ISkin skin, EzSkinEditorNotePreviewRequest request, EzManiaNotePart part);

        Drawable? CreateDrawableComparisonPreview(ISkin skin, EzSkinEditorNotePreviewRequest request);

        Drawable? CreateRulesetSettingsContent();
    }
}
