// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// In-memory Ez skin editor configuration snapshot for preview comparison and control reset baselines.
    /// Not persisted to disk — navigation triggers <see cref="Ez2ConfigManager"/> save separately.
    /// </summary>
    public sealed class EzSkinEditorComparisonSnapshot
    {
        public EzSkinJsonDocument Document { get; private set; } = new EzSkinJsonDocument();

        public void CaptureFrom(Ez2ConfigManager config) => Document = EzSkinJsonBridge.Capture(config);

        public void ApplyTo(Ez2ConfigManager config) => EzSkinJsonBridge.Apply(Document, config);

        public void SyncBindableDefaults(Ez2ConfigManager config) => EzSkinJsonBridge.SyncBindableDefaults(config, Document);
    }
}
