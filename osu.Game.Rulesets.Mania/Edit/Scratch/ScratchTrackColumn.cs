// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Rulesets.Mania.Edit.Scratch
{
    /// <summary>
    /// Renders a single background (scratch) track column.
    /// </summary>
    public partial class ScratchTrackColumn : CompositeDrawable
    {
        public const float WIDTH = 60;

        public ScratchTrackColumn(ScratchTrack track)
        {
            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;

            bool isMainAudio = track.Number == ScratchTrackSet.MAIN_AUDIO_TRACK_NUMBER;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = isMainAudio ? Colour4.CornflowerBlue.Opacity(0.25f) : Colour4.Black.Opacity(0.6f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Colour = Colour4.CornflowerBlue,
                    Alpha = isMainAudio ? 1 : 0,
                },
                new OsuSpriteText
                {
                    Text = $"#{track.Number}",
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding { Top = 6 },
                    Colour = isMainAudio ? Colour4.CornflowerBlue : Colour4.White.Opacity(0.75f),
                },
            };
        }
    }
}
