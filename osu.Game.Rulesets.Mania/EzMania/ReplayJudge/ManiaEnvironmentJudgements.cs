// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 按本局 <see cref="GameplayEnvironment"/> 绑定 Mania 物件 Judgement，与 <see cref="ManiaJudgementRound"/> / Kernel 同源。
    /// </summary>
    internal static class ManiaEnvironmentJudgements
    {
        public static Judgement CreateForTailNote(EzEnumHitMode hitMode)
        {
            if (MalodyHitModeJudgement.IsMalodyMode(hitMode))
                return new MalodyTailJudgement();

            return new ManiaJudgement();
        }

        public static void ApplyToBeatmap(IBeatmap beatmap, EzEnumHitMode hitMode)
        {
            foreach (var hitObject in beatmap.HitObjects)
                applyRecursive(hitObject, hitMode);
        }

        private static void applyRecursive(HitObject hitObject, EzEnumHitMode hitMode)
        {
            if (hitObject is TailNote)
                hitObject.SetJudgement(CreateForTailNote(hitMode));

            foreach (var nested in hitObject.NestedHitObjects)
                applyRecursive(nested, hitMode);
        }
    }
}
