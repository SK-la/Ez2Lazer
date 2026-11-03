// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Layout;

namespace osu.Game.EzOsuGame.Edit.Settings
{
    /// <summary>
    /// Global beatmap editor preferences on <see cref="EzConfigStore"/> (<c>BeatmapEditor.json</c>).
    /// </summary>
    public sealed class BeatmapEditorSettings
    {
        public const string FILENAME = "BeatmapEditor.json";

        public BeatmapEditorColouringSettings Colouring { get; } = new BeatmapEditorColouringSettings();
    }

    public sealed class BeatmapEditorColouringSettings
    {
        public ManiaEditorBeatLinePalette BeatLines { get; } = new ManiaEditorBeatLinePalette();
    }

    internal sealed class BeatmapEditorSettingsDocument
    {
        public BeatmapEditorColouringDocument? Colouring { get; set; }

        public static BeatmapEditorSettingsDocument From(BeatmapEditorSettings settings) => new BeatmapEditorSettingsDocument
        {
            Colouring = new BeatmapEditorColouringDocument
            {
                BeatLines = settings.Colouring.BeatLines.ToDocument(),
            },
        };

        public void ApplyTo(BeatmapEditorSettings settings)
        {
            settings.Colouring.BeatLines.ApplyDocument(Colouring?.BeatLines);
        }
    }

    internal sealed class BeatmapEditorColouringDocument
    {
        public Dictionary<string, string>? BeatLines { get; set; }
    }
}
