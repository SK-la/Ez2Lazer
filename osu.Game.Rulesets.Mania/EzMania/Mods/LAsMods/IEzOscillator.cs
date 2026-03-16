// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public interface IEzOscillator
    {
        double NextSigned();
        double Next();
        void Reset(long start = 0);
    }
}
