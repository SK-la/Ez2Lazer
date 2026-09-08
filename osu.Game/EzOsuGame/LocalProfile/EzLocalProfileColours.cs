// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Canonical Local Profile colour roles (from mania key expandable header).
    /// </summary>
    public static class EzLocalProfileColours
    {
        /// <summary>Key mode / primary category (e.g. 4K).</summary>
        public static Color4 KeyMode(OverlayColourProvider colours) => colours.Highlight1;

        /// <summary>Key-press / secondary count (e.g. total keys).</summary>
        public static Color4 KeyCount(OsuColour colours) => colours.BlueLight;

        /// <summary>Average rate (e.g. avg KPS, SSR axis).</summary>
        public static Color4 AvgRate(OsuColour colours) => colours.Orange1;

        /// <summary>Peak / max (e.g. max KPS, star peak).</summary>
        public static Color4 Peak(OsuColour colours) => colours.Yellow;

        /// <summary>Play count / primary body text.</summary>
        public static Color4 Plays(OverlayColourProvider colours) => colours.Content1;

        /// <summary>PP and score value emphasis.</summary>
        public static Color4 Pp(OsuColour colours) => colours.PinkLight;

        /// <summary>Duration / time.</summary>
        public static Color4 Duration(OsuColour colours) => colours.Lime1;

        /// <summary>Secondary meta (artist, date, captions).</summary>
        public static Color4 Meta(OverlayColourProvider colours) => colours.Content2;

        /// <summary>Generic numeric highlight when no finer role applies.</summary>
        public static Color4 Numeric(OverlayColourProvider colours) => colours.Highlight1;
    }
}
