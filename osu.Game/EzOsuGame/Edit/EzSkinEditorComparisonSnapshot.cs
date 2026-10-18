// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// In-memory skin editor configuration snapshot for preview comparison and control reset baselines.
    /// Covers <see cref="EzSkinJsonSettingCatalog"/> (Ez2Config) and <see cref="EzSkinIniFieldCatalog"/> (skin.ini draft).
    /// Does not include RulesetConfig, ScriptedSkin, or Note edit session state.
    /// Not persisted to disk — navigation triggers <see cref="Ez2ConfigManager"/> save separately.
    /// </summary>
    public sealed class EzSkinEditorComparisonSnapshot
    {
        public EzSkinJsonDocument Document { get; private set; } = new EzSkinJsonDocument();

        /// <summary>
        /// Captured <see cref="EzSkinIniSession.DraftText"/> when the session is supported; otherwise <c>null</c>.
        /// </summary>
        public string? SkinIniDraftText { get; private set; }

        /// <summary>
        /// Note preview width captured with the snapshot. Read as a value so comparison rendering never has to touch the live config.
        /// </summary>
        public double NotePreviewWidth { get; private set; }

        /// <summary>
        /// Note preview height captured with the snapshot (<see cref="Ez2Setting.NoteHeightScaleToWidth"/> applied).
        /// </summary>
        public double NotePreviewHeight { get; private set; }

        /// <summary>
        /// Note preview colour captured with the snapshot.
        /// </summary>
        public Colour4 NotePreviewColour { get; private set; } = Colour4.White;

        public void CaptureFrom(Ez2ConfigManager config, EzSkinIniSession? skinIniSession = null)
        {
            Document = EzSkinJsonBridge.Capture(config);
            SkinIniDraftText = skinIniSession is { IsSupported: true } ? skinIniSession.DraftText : null;

            double width = config.Get<double>(Ez2Setting.ColumnWidth);
            NotePreviewWidth = width;
            NotePreviewHeight = Math.Max(1, width * config.Get<double>(Ez2Setting.NoteHeightScaleToWidth));
            NotePreviewColour = config.Get<bool>(Ez2Setting.ColorSettingsEnabled)
                ? config.GetColumnColorByType(EzColumnType.A)
                : Colour4.White;
        }

        public void ApplyTo(Ez2ConfigManager config, EzSkinIniSession? skinIniSession = null)
        {
            EzSkinJsonBridge.Apply(Document, config);

            if (SkinIniDraftText != null && skinIniSession is { IsSupported: true })
                skinIniSession.SetDraftText(SkinIniDraftText);
        }

        public void SyncBindableDefaults(Ez2ConfigManager config) => EzSkinJsonBridge.SyncBindableDefaults(config, Document);
    }
}
