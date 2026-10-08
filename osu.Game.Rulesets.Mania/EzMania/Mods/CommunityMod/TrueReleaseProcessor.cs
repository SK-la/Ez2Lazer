// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    /// <summary>
    /// True Release 核心转换逻辑（纯函数）：把点/面按令牌算法互相焊接。
    /// 被 <see cref="ManiaModTrueRelease"/> 与对比 Mod 共用。
    /// </summary>
    internal static class TrueReleaseProcessor
    {
        /// <param name="input">原列上的 HitObjects（列号 0..n-1）。</param>
        /// <param name="minHoldMsAt">给定时间点，最短面长（毫秒）。</param>
        /// <param name="gapMsAt">给定时间点，新面尾与下一个对象之间的空隙（毫秒）。</param>
        /// <param name="minijackMsAt">给定时间点，avoid-minijack 阈值：面尾产生的头离下一个点产生的头小于该值时，面尾头放弃成头、变无。</param>
        public static List<ManiaHitObject> Process(
            IEnumerable<ManiaHitObject> input,
            Func<double, double> minHoldMsAt,
            Func<double, double> gapMsAt,
            Func<double, double> minijackMsAt)
        {
            var list = input.ToList();

            double commonEnd = list.Count == 0
                ? 0
                : list.Max(o => o is HoldNote h ? h.EndTime : o.StartTime);

            var output = new List<ManiaHitObject>();

            // 令牌 kind:
            // 0 = 永久点（列首无 → 点）
            // 1 = 点（含过短面塌缩）产生的新面头
            // 2 = 无（锚点）
            // 3 = 面尾产生的新面头
            foreach (var column in list.GroupBy(h => h.Column))
            {
                int col = column.Key;

                var tokens = new List<(double time, int kind, IList<HitSampleInfo> samples)>();

                foreach (var o in column.OrderBy(h => h.StartTime))
                {
                    double minHold = minHoldMsAt(o.StartTime);

                    if (o is HoldNote hold)
                    {
                        var headSamples = hold.GetNodeSamples(0);

                        if (hold.Duration < minHold)
                        {
                            // 过短的面 -> 点（继续参与焊接，和普通点一样）
                            tokens.Add((o.StartTime, 1, headSamples));
                        }
                        else
                        {
                            // 面头 -> 无，面尾 -> 面尾产生的新面头
                            tokens.Add((hold.StartTime, 2, Array.Empty<HitSampleInfo>()));
                            tokens.Add((hold.EndTime, 3, headSamples));
                        }
                    }
                    else
                    {
                        // 点 -> 点产生的新面头
                        tokens.Add((o.StartTime, 1, o.Samples));
                    }
                }

                // avoid minijack：面尾产生的头若离下一个“点产生的头”小于阈值，则放弃成头、变无
                for (int i = 0; i < tokens.Count - 1; i++)
                {
                    if (tokens[i].kind != 3)
                        continue;

                    var next = tokens[i + 1];

                    if (next.kind == 1 && next.time - tokens[i].time < minijackMsAt(tokens[i].time))
                        tokens[i] = (tokens[i].time, 2, Array.Empty<HitSampleInfo>());
                }

                // 列首的“无” -> 永久点
                if (tokens.Count > 0 && tokens[0].kind == 2)
                    tokens[0] = (tokens[0].time, 0, tokens[0].samples);

                // 公共结束时间补一个“无”，保证最后一个新面头能排到尾
                tokens.Add((commonEnd, 2, Array.Empty<HitSampleInfo>()));

                for (int i = 0; i < tokens.Count; i++)
                {
                    if (tokens[i].kind is not (1 or 3))
                        continue;

                    double headTime = tokens[i].time;
                    var headSamples = tokens[i].samples;
                    double gap = gapMsAt(headTime);
                    double minLen = minHoldMsAt(headTime);
                    var next = tokens[i + 1]; // 必然存在（已补 commonEnd）

                    double? tailTime = null;

                    if (next.kind == 2)
                    {
                        // 下一个是“无”：直接用它作面尾
                        tailTime = next.time;
                    }
                    else
                    {
                        // 下一个是头：在它前面 gap 处插入面尾
                        double proposed = next.time - gap;
                        if (proposed - headTime >= minLen)
                            tailTime = proposed;
                        // 否则插不下，降级为点
                    }

                    if (tailTime is double t && t - headTime >= minLen)
                    {
                        output.Add(new HoldNote
                        {
                            Column = col,
                            StartTime = headTime,
                            Duration = t - headTime,
                            NodeSamples = new List<IList<HitSampleInfo>>
                            {
                                headSamples,
                                Array.Empty<HitSampleInfo>(),
                            },
                        });
                    }
                    else
                    {
                        output.Add(new Note { Column = col, StartTime = headTime, Samples = headSamples });
                    }
                }

                // 保留永久点（列首无 -> 点）
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (tokens[i].kind == 0)
                        output.Add(new Note { Column = col, StartTime = tokens[i].time, Samples = tokens[i].samples });
                }
            }

            return output;
        }
    }
}
