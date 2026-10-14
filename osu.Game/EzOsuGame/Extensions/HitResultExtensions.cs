// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Extensions
{
    public static class HitResultExtensions
    {
        /// <summary>
        /// 获取指定 HitResult 在当前 JudgeMode 下的显示名称。
        /// </summary>
        public static LocalisableString GetJudgeModeDisplayName(this HitResult result)
            => result.GetJudgeModeDisplayName(GlobalConfigStore.EzConfig.Get<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode));

        /// <summary>
        /// 获取指定 HitResult 在给定 JudgeMode 下的显示名称。
        /// </summary>
        public static LocalisableString GetJudgeModeDisplayName(this HitResult result, EzEnumJudgeMode judgeMode)
        {
            string displayName = getDisplayNameForMode(result, judgeMode);

            if (string.IsNullOrEmpty(displayName))
                return result.GetLocalisableDescription();

            return displayName;
        }

        private static string getDisplayNameForMode(HitResult result, EzEnumJudgeMode judgeMode)
        {
            switch (judgeMode)
            {
                case EzEnumJudgeMode.Lazer:
                case EzEnumJudgeMode.Classic:
                    // Lazer 和 Classic 使用默认名称
                    return string.Empty;

                case EzEnumJudgeMode.O2Jam:
                    return getO2JamDisplayName(result);

                case EzEnumJudgeMode.EZ2AC:
                    return getEZ2ACDisplayName(result);

                case EzEnumJudgeMode.IIDX_HD:
                case EzEnumJudgeMode.LR2_HD:
                case EzEnumJudgeMode.Raja_NM:
                    // BMS 系列使用相同的命名
                    return getBMSDisplayName(result);

                case EzEnumJudgeMode.Malody_E:
                case EzEnumJudgeMode.Malody_B:
                    // Malody 使用相同的命名
                    return getMalodyDisplayName(result);

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// O2Jam 判定名称映射
        /// Perfect(305) -> Cool
        /// Great(300) -> (空,不使用)
        /// Good(200) -> Good
        /// Ok(100) -> (空,不使用)
        /// Meh(50) -> Bad
        /// Miss -> Miss
        /// </summary>
        private static string getO2JamDisplayName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return "Cool";

                case HitResult.Good:
                    return "Good";

                case HitResult.Meh:
                    return "Bad";

                case HitResult.Miss:
                    return "Miss";

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// EZ2AC 判定名称映射
        /// Perfect(305) -> Kool
        /// Great(300) -> Cool
        /// Good(200) -> Good
        /// Ok(100) -> (空,不使用)
        /// Meh(50) -> Miss
        /// Miss -> Fail
        /// </summary>
        private static string getEZ2ACDisplayName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return "Kool";

                case HitResult.Great:
                    return "Cool";

                case HitResult.Good:
                    return "Good";

                case HitResult.Meh:
                    return "Miss";

                case HitResult.Miss:
                    return "Fail";

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// BMS 系列(IIDX/LR2/Raja) 判定名称映射
        /// Perfect(305) -> Kool
        /// Great(300) -> Cool
        /// Good(200) -> Good
        /// Ok(100) -> (空,不使用)
        /// Meh(50) -> Bad
        /// Miss -> Poor
        /// Poor -> KPoor
        /// </summary>
        private static string getBMSDisplayName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return "Perfect";

                case HitResult.Great:
                    return "Great";

                case HitResult.Good:
                    return "Good";

                case HitResult.Meh:
                    return "Bad";

                case HitResult.Miss:
                    return "Poor";

                case HitResult.Poor:
                    return "KPoor";

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Malody 判定名称映射
        /// Perfect(305) -> Best
        /// Great(300) -> Cool
        /// Good(200) -> Good
        /// Ok(100) -> (空,不使用)
        /// Meh(50) -> (空,不使用)
        /// Miss -> Miss
        /// </summary>
        private static string getMalodyDisplayName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return "Best";

                case HitResult.Great:
                    return "Cool";

                case HitResult.Good:
                    return "Good";

                case HitResult.Miss:
                    return "Miss";

                default:
                    return string.Empty;
            }
        }
    }
}
