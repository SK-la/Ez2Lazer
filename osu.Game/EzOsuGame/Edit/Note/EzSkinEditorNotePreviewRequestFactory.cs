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
            EzManiaNotePart exportPart = EzManiaNotePart.Note) =>
            new EzSkinEditorNotePreviewRequest
            {
                UseEzNoteVariants = useEzNoteVariants,
                CompareKind = compareKind,
                Ruleset = ruleset,
                Part = exportPart,
                VariantId = variantId,
                NoteColour = resolveConfigNoteColour(config),
                Width = config.Get<double>(Ez2Setting.ColumnWidth),
                Height = Math.Max(1, config.Get<double>(Ez2Setting.ColumnWidth) * config.Get<double>(Ez2Setting.NoteHeightScaleToWidth)),
            };

        private static Colour4 resolveConfigNoteColour(Ez2ConfigManager config) =>
            config.Get<bool>(Ez2Setting.ColorSettingsEnabled)
                ? config.GetColumnColorByType(EzColumnType.A)
                : Colour4.White;

        /// <summary>
        /// Builds a request from values captured in <paramref name="snapshot"/> only. Never reads or writes the live config:
        /// applying a snapshot to <see cref="Ez2ConfigManager"/> here would feed back into config change listeners.
        /// </summary>
        public static EzSkinEditorNotePreviewRequest FromSnapshot(
            EzSkinEditorComparisonSnapshot snapshot,
            bool useEzNoteVariants,
            EzSkinEditorNoteCompareKind compareKind,
            string variantId,
            RulesetInfo ruleset,
            EzManiaNotePart exportPart = EzManiaNotePart.Note) =>
            new EzSkinEditorNotePreviewRequest
            {
                UseEzNoteVariants = useEzNoteVariants,
                CompareKind = compareKind,
                Ruleset = ruleset,
                Part = exportPart,
                VariantId = variantId,
                NoteColour = snapshot.NotePreviewColour,
                Width = snapshot.NotePreviewWidth,
                Height = snapshot.NotePreviewHeight,
            };
    }
}
