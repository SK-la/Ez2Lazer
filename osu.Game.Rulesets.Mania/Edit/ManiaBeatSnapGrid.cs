// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.Graphics;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Edit
{
    public partial class ManiaBeatSnapGrid : BeatSnapGrid
    {
        [Resolved(CanBeNull = true)]
        private ManiaEditorDivisorPaletteBinding? divisorPalette { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        protected override void LoadComplete()
        {
            divisorPalette?.Palette?.EnsureDefaults(colours);

            base.LoadComplete();

            if (divisorPalette != null)
                divisorPalette.Changed += InvalidateLines;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (divisorPalette != null)
                divisorPalette.Changed -= InvalidateLines;

            base.Dispose(isDisposing);
        }

        protected override Color4 GetLineColour(int divisor)
            => ManiaEditorBeatLinePalette.LineColour(divisor, colours, divisorPalette?.Palette);

        protected override IEnumerable<Container> GetTargetContainers(HitObjectComposer composer)
        {
            return ((ManiaPlayfield)composer.Playfield)
                   .Stages
                   .SelectMany(stage => stage.Columns)
                   .Select(column => column.HitObjectArea.BeatSnapGridTarget);
        }
    }
}
