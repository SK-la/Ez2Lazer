// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.EzOsuGame.Layout;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Shared <see cref="ManiaEditorBeatLinePalette"/> from <see cref="EzConfigStore"/> (<c>BeatmapEditor.json</c>) and change notifications.
    /// </summary>
    public sealed class ManiaEditorDivisorPaletteBinding
    {
        public ManiaEditorBeatLinePalette? Palette { get; }

        public event Action? Changed;

        public ManiaEditorDivisorPaletteBinding(EzConfigStore? configStore)
        {
            if (configStore == null)
                return;

            Palette = configStore.BeatmapEditor.Colouring.BeatLines;

            foreach (int divisor in ManiaEditorBeatLinePalette.EDITOR_LINE_DIVISORS)
                Palette.GetBindable(divisor).BindValueChanged(_ => Changed?.Invoke(), false);
        }
    }
}
