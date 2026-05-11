// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 将 <see cref="HitResult"/> 映射到 GameTheme/{theme}/judgement/ 下的资源文件夹名。
    /// 各机种命名风格用一组独立的模板表达，通过 <see cref="EzEnumHitMode"/> 选择激活的模板。
    /// </summary>
    /// <remarks>
    /// 返回的字符串需对应 <c>GameTheme/{theme}/judgement/{name}/</c> 子目录名或同级帧前缀，
    /// 留空表示当前判定类型在该机种下不渲染（例如 EZ2AC 没有独立的 Ok 判定）。
    /// </remarks>
    public static class EzHitResultNameTemplate
    {
        /// <summary>
        /// 取得在指定模板下，<paramref name="result"/> 对应的资源文件夹名。
        /// </summary>
        public static string GetResourceName(EzEnumHitMode template, HitResult result)
        {
            switch (template)
            {
                case EzEnumHitMode.EZ2AC:
                    return getEz2AcName(result);

                case EzEnumHitMode.O2Jam:
                    return getO2JamName(result);

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    return getBmsName(result);

                case EzEnumHitMode.Malody_E:
                case EzEnumHitMode.Malody_B:
                    return getMalodyName(result);

                case EzEnumHitMode.Lazer:
                case EzEnumHitMode.Classic:
                default:
                    return getLazerName(result);
            }
        }

        /// <summary>
        /// 当某个判定资源找不到时，按优先级返回回退的判定名称。
        /// 根据 wiki 规范的通用回退规则（与模板无关）：
        /// - Bad 找不到 → Miss
        /// - Miss 找不到 → Fail
        /// - Fail 找不到 → 跳过显示
        /// - Poor 找不到 → Fail
        /// - Poor 不会作为其他判定的回退目标
        /// </summary>
        /// <param name="template">当前使用的命名模板</param>
        /// <param name="missingResult">缺失资源的原始判定类型</param>
        /// <returns>回退的判定资源名，若无可用的回退则返回空字符串</returns>
        public static string GetFallbackResourceName(EzEnumHitMode template, HitResult missingResult)
        {
            // 获取原始资源名称
            string originalName = GetResourceName(template, missingResult);

            if (string.IsNullOrEmpty(originalName))
                return string.Empty;

            // 根据资源名称应用通用回退规则
            return originalName switch
            {
                "Bad" => "Miss",           // Bad 找不到 → Miss
                "Miss" => "Fail",          // Miss 找不到 → Fail
                "Fail" => string.Empty,    // Fail 找不到 → 跳过
                "Poor" => "Fail",          // Poor 找不到 → Fail
                _ => string.Empty          // 其他情况不自动回退
            };
        }

        // Lazer / Classic 命名风格：与 HitResult 名一一对应
        private static string getLazerName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return "Kool";

                case HitResult.Great:   return "Cool";

                case HitResult.Good:    return "Good";

                case HitResult.Ok:      return "Ok";

                case HitResult.Meh:     return "Bad";

                case HitResult.Miss:    return "Miss";

                case HitResult.Poor:    return "Poor";

                default:                return string.Empty;
            }
        }

        // EZ2AC: Kool / Cool / Good / (Meh=>Miss) / (Miss=>Fail)
        private static string getEz2AcName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return "Kool";

                case HitResult.Great:   return "Cool";

                case HitResult.Good:    return "Good";

                case HitResult.Meh:     return "Miss";

                case HitResult.Miss:
                case HitResult.Poor:    return "Fail";

                default:                return string.Empty;
            }
        }

        // O2Jam: Cool / Good / Bad / Miss
        private static string getO2JamName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return "Cool";

                case HitResult.Good:    return "Good";

                case HitResult.Meh:     return "Bad";

                case HitResult.Miss:
                case HitResult.Poor:    return "Miss";

                default:                return string.Empty;
            }
        }

        // BMS (IIDX / LR2 / Raja): Kool / Cool / Good / Bad / Poor / KPoor
        private static string getBmsName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return "Kool";

                case HitResult.Great:   return "Cool";

                case HitResult.Good:    return "Good";

                case HitResult.Meh:     return "Miss";

                case HitResult.Miss:    return "Fail";

                case HitResult.Poor:    return "Poor";

                default:                return string.Empty;
            }
        }

        // Malody: Best / Cool / Good / Miss
        private static string getMalodyName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return "Kool";

                case HitResult.Great:   return "Cool";

                case HitResult.Good:    return "Good";

                case HitResult.Meh:
                case HitResult.Miss:
                case HitResult.Poor:    return "Miss";

                default:                return string.Empty;
            }
        }
    }
}
