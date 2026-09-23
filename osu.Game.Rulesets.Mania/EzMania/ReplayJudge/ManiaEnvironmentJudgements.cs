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
            // Malody / EZ2AC：尾不单独计分（无松手窗），仅 Ignore 完结。
            if (MalodyHitModeJudgement.IsMalodyMode(hitMode) || hitMode == EzEnumHitMode.EZ2AC)
                return new MalodyTailJudgement();

            return new ManiaJudgement();
        }

        public static Judgement CreateForHoldNoteTick(EzEnumHitMode hitMode)
        {
            if (hitMode == EzEnumHitMode.EZ2AC)
                return new HoldNoteTickJudgement();

            // 其它模式：tick 仍存在于 nested 树中，Ignore 完结以免卡 AllJudged，且不计分。
            return new IgnoreJudgement();
        }

        public static void ApplyToBeatmap(IBeatmap beatmap, EzEnumHitMode hitMode)
        {
            foreach (var hitObject in beatmap.HitObjects)
                applyRecursive(hitObject, hitMode);
        }

        private static void applyRecursive(HitObject hitObject, EzEnumHitMode hitMode)
        {
            switch (hitObject)
            {
                case TailNote:
                    hitObject.SetJudgement(CreateForTailNote(hitMode));
                    break;

                case HoldNoteTick:
                    hitObject.SetJudgement(CreateForHoldNoteTick(hitMode));
                    break;
            }

            foreach (var nested in hitObject.NestedHitObjects)
                applyRecursive(nested, hitMode);
        }
    }
}
