// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum EzEnumHitResult
    {
        [Description("Perfect")]
        Perfect,

        [Description("Great")]
        Great,

        [Description("Good")]
        Good,

        [Description("Meh")]
        Meh,

        [Description("Miss")]
        Miss,

        [Description("KPoor")]
        Poor,
    }

    public static class EzEnumHitResultExtensions
    {
        public static HitResult ToHitResult(this EzEnumHitResult result)
        {
            return result switch
            {
                EzEnumHitResult.Perfect => HitResult.Perfect,
                EzEnumHitResult.Great => HitResult.Great,
                EzEnumHitResult.Good => HitResult.Good,
                EzEnumHitResult.Meh => HitResult.Meh,
                EzEnumHitResult.Miss => HitResult.Miss,
                EzEnumHitResult.Poor => HitResult.Poor,
                _ => HitResult.None
            };
        }

        public static EzEnumHitResult FromHitResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => EzEnumHitResult.Perfect,
                HitResult.Great => EzEnumHitResult.Great,
                HitResult.Good => EzEnumHitResult.Good,
                HitResult.Meh => EzEnumHitResult.Meh,
                HitResult.Miss => EzEnumHitResult.Miss,
                _ => EzEnumHitResult.Poor
            };
        }

        /// <summary>
        /// Gets the ordered index for display purposes. Lower index means better judgement quality.
        /// </summary>
        public static int GetIndexForOrderedDisplay(this EzEnumHitResult result)
        {
            return result switch
            {
                EzEnumHitResult.Perfect => 0,
                EzEnumHitResult.Great => 1,
                EzEnumHitResult.Good => 2,
                EzEnumHitResult.Meh => 4,
                EzEnumHitResult.Miss => 5,
                EzEnumHitResult.Poor => 6,
                _ => -1
            };
        }
    }
}
