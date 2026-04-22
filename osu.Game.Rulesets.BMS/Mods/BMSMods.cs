// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.BMS.Mods
{
    public class BMSModEasy : ModEasy
    {
        public override LocalisableString Description => "Wider hit windows for a more lenient experience.";
        public override double ScoreMultiplier => 0.5;
    }

    public class BMSModNoFail : ModNoFail
    {
        public override LocalisableString Description => "You can't fail, no matter what.";
        public override double ScoreMultiplier => 0.5;
    }

    public class BMSModHardRock : ModHardRock
    {
        public override LocalisableString Description => "Tighter hit windows for a more challenging experience.";
        public override double ScoreMultiplier => 1.06;
    }

    public class BMSModSuddenDeath : ModSuddenDeath
    {
        public override LocalisableString Description => "Miss and fail.";
    }

    public class BMSModAutoplay : ModAutoplay
    {
        public override LocalisableString Description => "Watch a perfect automated play through the song.";
    }

    public class BMSModRandom : Mod
    {
        public override string Name => "Random";
        public override string Acronym => "RD";
        public override ModType Type => ModType.Fun;
        public override LocalisableString Description => "Randomize the lane positions of notes.";
        public override double ScoreMultiplier => 1;
    }

    public class BMSModMirror : Mod
    {
        public override string Name => "Mirror";
        public override string Acronym => "MR";
        public override ModType Type => ModType.Fun;
        public override LocalisableString Description => "Flip the playfield horizontally.";
        public override double ScoreMultiplier => 1;
    }
}
