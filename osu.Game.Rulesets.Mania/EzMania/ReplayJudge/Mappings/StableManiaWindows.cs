// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// osu!stable mania 原生判定窗口（OD 窗口 / 转谱窗口 + EZ·HR·DT·HT 乘窗 + (int) 截断）。
    /// 仅供 StableClassic 判定档使用。
    /// </summary>
    public readonly struct StableManiaWindows
    {
        public readonly double Perfect300g;
        public readonly double Great300;
        public readonly double Good200;
        public readonly double Ok100;
        public readonly double Meh50;
        public readonly double Miss;

        private StableManiaWindows(double perfect300g, double great300, double good200, double ok100, double meh50, double miss)
        {
            Perfect300g = perfect300g;
            Great300 = great300;
            Good200 = good200;
            Ok100 = ok100;
            Meh50 = meh50;
            Miss = miss;
        }

        /// <param name="overallDifficulty">未取倒数的 OD（Easy/HR 已由调用方折入）。</param>
        /// <param name="windowMultiplier">stable 的乘窗系数 = speed / difficulty（DT/HT 与 EZ/HR 的合成）。</param>
        /// <param name="isConvert">是否为非 mania 原谱转换而来的谱面。</param>
        public static StableManiaWindows Create(double overallDifficulty, double windowMultiplier, bool isConvert)
        {
            double r300g;
            double r300;
            double r200;
            double r100;
            double r50;
            double rMiss;

            if (isConvert)
            {
                // 转谱：OD 高低两档常数窗。
                bool highOd = Math.Round(overallDifficulty) > 4;
                r300g = 16;
                r300 = highOd ? 34 : 47;
                r200 = highOd ? 67 : 77;
                r100 = 97;
                r50 = 121;
                rMiss = 158;
            }
            else
            {
                double invertedOd = Math.Clamp(10 - overallDifficulty, 0, 10);
                r300g = 16;
                r300 = 34 + 3 * invertedOd;
                r200 = 67 + 3 * invertedOd;
                r100 = 97 + 3 * invertedOd;
                r50 = 121 + 3 * invertedOd;
                rMiss = 158 + 3 * invertedOd;
            }

            return new StableManiaWindows(
                (int)(r300g * windowMultiplier),
                (int)(r300 * windowMultiplier),
                (int)(r200 * windowMultiplier),
                (int)(r100 * windowMultiplier),
                (int)(r50 * windowMultiplier),
                (int)(rMiss * windowMultiplier));
        }

        /// <summary>
        /// 按已烘焙好的窗口值重建，供 Live / Session 从 <c>ManiaHitWindows</c> 直接取值，避免再算一遍 OD / mods。
        /// </summary>
        public static StableManiaWindows FromRanges(double perfect300g, double great300, double good200, double ok100, double meh50, double miss)
            => new StableManiaWindows(perfect300g, great300, good200, ok100, meh50, miss);

        /// <summary>普通 note：按判定时间的绝对偏差取档（stable 用 &lt;=）。</summary>
        public StableManiaJudgement Judge(double offset)
        {
            double abs = Math.Abs(offset);

            if (abs <= Perfect300g)
                return StableManiaJudgement.Perfect300g;
            if (abs <= Great300)
                return StableManiaJudgement.Great300;
            if (abs <= Good200)
                return StableManiaJudgement.Good200;
            if (abs <= Ok100)
                return StableManiaJudgement.Ok100;
            if (abs <= Meh50)
                return StableManiaJudgement.Meh50;

            return StableManiaJudgement.Miss;
        }

        /// <summary>长条：stable 用「头偏差」与「头尾偏差和」双条件联合取档。</summary>
        public StableManiaJudgement JudgeLong(double headOffset, double tailOffset)
        {
            double head = Math.Abs(headOffset);
            double combined = head + Math.Abs(tailOffset);

            if (head <= Perfect300g * 1.2 && combined <= Perfect300g * 2.4)
                return StableManiaJudgement.Perfect300g;
            if (head <= Great300 * 1.1 && combined <= Great300 * 2.2)
                return StableManiaJudgement.Great300;
            if (head <= Good200 && combined <= Good200 * 2)
                return StableManiaJudgement.Good200;
            if (head <= Ok100 && combined <= Ok100 * 2)
                return StableManiaJudgement.Ok100;
            if (head <= Meh50 && combined <= Meh50 * 2)
                return StableManiaJudgement.Meh50;

            return StableManiaJudgement.Miss;
        }

        /// <summary>
        /// stable classic 长条判定。
        /// </summary>
        /// <remarks>
        /// 用 <b>最近</b>按下 / <b>首次有效</b>松开时刻，而不是触发本次判定的那次边沿：
        /// <list type="bullet">
        /// <item>按下基准：早于 <c>StartTime - HitWindow50</c> 的按下镜像到段末（等于整段没接上），否则镜像到段首。</item>
        /// <item>松开基准：按住时取当前时刻，否则取首次「不早于 <c>EndTime - HitWindow50</c>」的松开时刻并镜像到段末。</item>
        /// <item>只有松开基准早于 <c>EndTime - HitWindow50</c> 才是 Miss——也就是「从没按下」或「提前放掉」；
        /// 其余一律按档位链取档，最低 50。</item>
        /// <item>中途断连过的长条再抓回来并正常松手时，档位封到 Good200，不会再往上抬。</item>
        /// </list>
        /// </remarks>
        public StableManiaJudgement JudgeLongClassic(double startTime, double endTime, double timePress, double timeRelease, bool stillPressed, double evaluationTime, bool holdBroken = false)
        {
            int start = (int)startTime;
            int end = (int)endTime;
            int press = (int)timePress;
            int release = (int)timeRelease;
            int now = (int)evaluationTime;

            int pressBaseline = press < start - Meh50
                ? end - 1
                : (press >= start ? press : start + (start - press));
            int releaseBaseline = stillPressed
                ? now
                : (release <= end ? release : end - (release - end));

            if (releaseBaseline < end - Meh50)
                return StableManiaJudgement.Miss;

            int pressError = Math.Abs(pressBaseline - start);
            int releaseError = Math.Abs(releaseBaseline - end);
            int combinedError = pressError + releaseError;

            StableManiaJudgement judgement;

            if (pressError <= Perfect300g * 1.2 && combinedError <= Perfect300g * 2.4)
                judgement = StableManiaJudgement.Perfect300g;
            else if (pressError <= Great300 * 1.1 && combinedError <= Great300 * 2.2)
                judgement = StableManiaJudgement.Great300;
            else if (pressError <= Good200 && combinedError <= Good200 * 2)
                judgement = StableManiaJudgement.Good200;
            else if (pressError <= Ok100 && combinedError <= Ok100 * 2)
                judgement = StableManiaJudgement.Ok100;
            else
                judgement = StableManiaJudgement.Meh50;

            if (holdBroken && judgement > StableManiaJudgement.Good200)
                judgement = StableManiaJudgement.Good200;

            return judgement;
        }
    }
}
