// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.BMS.Objects
{
    /// <summary>
    /// Mania note with BMS keysound samples attached.
    /// </summary>
    public class BmsManiaNote : Note, IBmsKeysoundProvider
    {
        public bool IsScratch { get; init; }

        public List<HitSampleInfo> KeysoundSamples { get; init; } = new List<HitSampleInfo>();

        IReadOnlyList<HitSampleInfo> IBmsKeysoundProvider.KeysoundSamples => KeysoundSamples;

        public override IList<HitSampleInfo> AuxiliarySamples => KeysoundSamples;
    }
}
