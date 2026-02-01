// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Represents a background (non-note) sound event in BMS.
    /// </summary>
    public class BmsBackgroundSoundEvent
    {
        public BmsBackgroundSoundEvent(double time, string filename)
        {
            Time = time;
            Filename = filename;
        }

        public double Time { get; }

        public string Filename { get; }
    }
}
