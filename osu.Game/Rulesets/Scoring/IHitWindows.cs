// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Scoring
{
    public interface IHitWindows : IApplicableMod
    {
        bool IsHitResultAllowed(HitResult result);
        // double WindowFor(HitResult result);

        // internal DifficultyRange[] GetRanges();
        // public DifficultyRange[] GetRanges() => BaseRanges;

        // public static DifficultyRange[] BaseRanges =
        // {
        //     new DifficultyRange(HitResult.Perfect, 22.4D, 19.4D, 13.9D),
        //     new DifficultyRange(HitResult.Great, 64, 49, 34),
        //     new DifficultyRange(HitResult.Good, 97, 82, 67),
        //     new DifficultyRange(HitResult.Ok, 127, 112, 97),
        //     new DifficultyRange(HitResult.Meh, 151, 136, 121),
        //     new DifficultyRange(HitResult.Miss, 188, 173, 158),
        // };

        void SetHitWindows(double window);

        void SetDifficulty(double difficulty);

        void ResetHitWindows();
    }

    public static class HitWindowsExtensions
    {
        public static void SetDifficultyRange(this HitWindows windows, double perfect, double great, double good, double ok, double meh, double miss)
        {
            if (windows is DefaultHitWindows defaultWindows)
            {
                defaultWindows.SetCustomWindows(perfect, great, good, ok, meh, miss);
            }
        }

        public static void ResetHitWindows(this HitWindows windows)
        {
            if (windows is DefaultHitWindows defaultWindows)
            {
                defaultWindows.ResetHitWindows();
            }
        }
    }
}
