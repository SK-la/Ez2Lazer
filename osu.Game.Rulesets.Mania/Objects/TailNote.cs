// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;

// ReSharper disable InconsistentNaming

namespace osu.Game.Rulesets.Mania.Objects
{
    /// <summary>
    /// The tail note of a <see cref="HoldNote"/>.
    /// </summary>
    public class TailNote : Note
    {
        /// <summary>
        /// Lenience of release hit windows. This is to make cases where the hold note release
        /// is timed alongside presses of other hit objects less awkward.
        /// Todo: This shouldn't exist for non-LegacyBeatmapDecoder beatmaps
        /// </summary>
        public static double RELEASE_WINDOW_LENIENCE { get; set; } = 1.5D;

        public override Judgement CreateJudgement()
        {
            var hitMode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);

            switch (hitMode)
            {
                case EzEnumHitMode.EZ2AC:
                case EzEnumHitMode.Malody:
                    return new HoldNoteBodyJudgement();

                default:
                    return new ManiaJudgement();
            }
        }

        public override double MaximumJudgementOffset => base.MaximumJudgementOffset * RELEASE_WINDOW_LENIENCE;
    }
}
