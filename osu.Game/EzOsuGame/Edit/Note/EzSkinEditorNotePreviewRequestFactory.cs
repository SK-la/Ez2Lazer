// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
namespace osu.Game.EzOsuGame.Edit.Note
{
    public static class EzSkinEditorNotePreviewRequestFactory
    {
        public static EzSkinEditorNotePreviewRequest FromConfig(
            Ez2ConfigManager config,
            bool useEzNoteVariants,
            EzSkinEditorNoteCompareKind compareKind,
            string variantId,
            RulesetInfo ruleset,
            EzSkinEditorNotePart exportPart = EzSkinEditorNotePart.Note) =>
            new EzSkinEditorNotePreviewRequest
            {
                UseEzNoteVariants = useEzNoteVariants,
                CompareKind = compareKind,
                Ruleset = ruleset,
                Part = exportPart,
                VariantId = variantId,
                NoteColour = Colour4.White,
                Width = config.Get<double>(Ez2Setting.ColumnWidth),
                Height = Math.Max(1, config.Get<double>(Ez2Setting.ColumnWidth) * config.Get<double>(Ez2Setting.NoteHeightScaleToWidth)),
            };

        public static EzSkinEditorNotePreviewRequest FromDocument(
            EzSkinJsonDocument document,
            Ez2ConfigManager config,
            bool useEzNoteVariants,
            EzSkinEditorNoteCompareKind compareKind,
            string variantId,
            RulesetInfo ruleset,
            EzSkinEditorNotePart exportPart = EzSkinEditorNotePart.Note)
        {
            var saved = EzSkinJsonBridge.Capture(config);

            try
            {
                EzSkinJsonBridge.Apply(document, config);
                return FromConfig(config, useEzNoteVariants, compareKind, variantId, ruleset, exportPart);
            }
            finally
            {
                EzSkinJsonBridge.Apply(saved, config);
            }
        }
    }
}
