// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Audio;

namespace osu.Game.Rulesets.BMS.Objects
{
    /// <summary>
    /// Provides keysound samples for BMS objects.
    /// </summary>
    public interface IBmsKeysoundProvider
    {
        IReadOnlyList<HitSampleInfo> KeysoundSamples { get; }
    }
}
