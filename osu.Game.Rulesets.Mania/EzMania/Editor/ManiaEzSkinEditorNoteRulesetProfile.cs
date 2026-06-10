// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Note;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class ManiaEzSkinEditorNoteRulesetProfile : IEzSkinEditorNoteRulesetProfile
    {
        private static readonly EzSkinEditorNotePart[] supported_parts =
        {
            EzSkinEditorNotePart.Note,
            EzSkinEditorNotePart.HoldHead,
            EzSkinEditorNotePart.HoldBody,
            EzSkinEditorNotePart.HoldTail,
        };

        private static readonly EzSkinEditorNoteVariant[] legacy_variants =
        {
            new EzSkinEditorNoteVariant("1", "1"),
            new EzSkinEditorNoteVariant("2", "2"),
            new EzSkinEditorNoteVariant("S", "S"),
        };

        private static readonly EzSkinEditorNoteVariant[] ez_variants =
        {
            new EzSkinEditorNoteVariant(nameof(EzColumnType.A), "A"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.B), "B"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.S), "S"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.E), "E"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.P), "P"),
        };

        public int RulesetOnlineId => new ManiaRuleset().RulesetInfo.OnlineID;

        public RulesetInfo RulesetInfo => new ManiaRuleset().RulesetInfo;

        public IReadOnlyList<EzSkinEditorNotePart> SupportedParts => supported_parts;

        public IReadOnlyList<EzSkinEditorNoteVariant> GetVariants(bool useEzNoteVariants, EzSkinEditorNotePart part) =>
            useEzNoteVariants ? ez_variants : legacy_variants;

        public string GetDefaultVariantId(bool useEzNoteVariants, EzSkinEditorNotePart part) =>
            useEzNoteVariants ? nameof(EzColumnType.A) : "1";

        public string ResolveTextureName(bool useEzNoteVariants, EzSkinEditorNotePart part, string variantId) =>
            useEzNoteVariants
                ? resolveEzTextureName(part, variantId)
                : resolveLegacyTextureName(part, variantId);

        public Drawable? CreateRulesetSettingsContent() => null;

        private static string resolveLegacyTextureName(EzSkinEditorNotePart part, string variantId)
        {
            string index = variantId switch
            {
                "2" => "2",
                "S" => "S",
                _ => "1",
            };

            return part switch
            {
                EzSkinEditorNotePart.HoldHead => $"mania-note{index}H",
                EzSkinEditorNotePart.HoldBody => $"mania-note{index}L",
                EzSkinEditorNotePart.HoldTail => $"mania-note{index}T",
                _ => $"mania-note{index}",
            };
        }

        private static string resolveEzTextureName(EzSkinEditorNotePart part, string variantId)
        {
            string prefix = variantId switch
            {
                nameof(EzColumnType.B) => "blue",
                nameof(EzColumnType.S) or nameof(EzColumnType.P) => "green",
                _ => "white",
            };

            return part switch
            {
                EzSkinEditorNotePart.HoldHead => $"{prefix}longnote/head",
                EzSkinEditorNotePart.HoldBody => $"{prefix}longnote/middle",
                EzSkinEditorNotePart.HoldTail => $"{prefix}longnote/tail",
                _ => $"{prefix}note",
            };
        }
    }
}
