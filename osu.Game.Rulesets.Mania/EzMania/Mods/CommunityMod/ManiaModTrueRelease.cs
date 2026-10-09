// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public enum TrueReleaseUnit
    {
        Beats,
        Milliseconds,
    }

    /// <summary>
    /// 点/面互相焊接：消掉面头、把点和面尾拉成新面，短面塌缩回点，所有列对齐到公共结束时间。
    /// </summary>
    public class ManiaModTrueRelease : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "True Inverse";

        public override string Acronym => "TI";

        public override LocalisableString Description => TrueReleaseStrings.TRUE_RELEASE_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.ExpandArrowsAlt;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(TrueReleaseStrings), nameof(TrueReleaseStrings.MIN_HOLD_LENGTH_LABEL), nameof(TrueReleaseStrings.MIN_HOLD_LENGTH_DESCRIPTION))]
        public BindableDouble MinHoldLength { get; } = new BindableDouble(0.25)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource(typeof(TrueReleaseStrings), nameof(TrueReleaseStrings.TAIL_GAP_LABEL), nameof(TrueReleaseStrings.TAIL_GAP_DESCRIPTION))]
        public BindableDouble TailGap { get; } = new BindableDouble(0.5)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource(typeof(TrueReleaseStrings), nameof(TrueReleaseStrings.MINIJACK_LABEL), nameof(TrueReleaseStrings.MINIJACK_DESCRIPTION))]
        public BindableDouble Minijack { get; } = new BindableDouble(0.5)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource(typeof(TrueReleaseStrings), nameof(TrueReleaseStrings.UNIT_LABEL), nameof(TrueReleaseStrings.UNIT_DESCRIPTION))]
        public Bindable<TrueReleaseUnit> Unit { get; } = new Bindable<TrueReleaseUnit>(TrueReleaseUnit.Beats);

        public ManiaModTrueRelease()
        {
            Unit.BindValueChanged(_ => applyUnitRange(), true);
        }

        private void applyUnitRange()
        {
            bool ms = Unit.Value == TrueReleaseUnit.Milliseconds;
            double beatsPrecision = 1.0 / 12.0;

            applyRange(MinHoldLength, ms, 160, 0.25, beatsPrecision);
            applyRange(TailGap, ms, 160, 0.5, beatsPrecision);
            applyRange(Minijack, ms, 200, 0.5, beatsPrecision);
        }

        private static void applyRange(BindableDouble b, bool ms, double msDefault, double beatsDefault, double beatsPrecision)
        {
            b.MinValue = 0;
            b.MaxValue = ms ? 1000 : 4;
            b.Precision = ms ? 10 : beatsPrecision;
            b.Value = ms ? msDefault : beatsDefault;
        }

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (TrueReleaseStrings.MIN_HOLD_LENGTH_LABEL, $"{MinHoldLength.Value:0.##} {Unit}");
                yield return (TrueReleaseStrings.TAIL_GAP_LABEL, $"{TailGap.Value:0.##} {Unit}");
                yield return (TrueReleaseStrings.MINIJACK_LABEL, $"{Minijack.Value:0.##} {Unit}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        private double resolveMs(BindableDouble v, double atTime, IBeatmap beatmap)
            => Unit.Value == TrueReleaseUnit.Milliseconds
                ? v.Value
                : v.Value * beatmap.ControlPointInfo.TimingPointAt(atTime).BeatLength;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var mania = (ManiaBeatmap)beatmap;
            mania.HitObjects = Process(
                mania.HitObjects,
                t => resolveMs(MinHoldLength, t, beatmap),
                t => resolveMs(TailGap, t, beatmap),
                t => resolveMs(Minijack, t, beatmap)).OrderBy(h => h.StartTime).ToList();
        }

        /// <summary>
        /// True Inverse 核心转换逻辑（纯函数）：把点/面按令牌算法互相焊接。
        /// </summary>
        /// <param name="input">原列上的 HitObjects（列号 0..n-1）。</param>
        /// <param name="minHoldMsAt">给定时间点，最短面长（毫秒）。</param>
        /// <param name="gapMsAt">给定时间点，新面尾与下一个对象之间的空隙（毫秒）。</param>
        /// <param name="minijackMsAt">给定时间点，avoid-minijack 阈值：面尾产生的头离下一个点产生的头小于该值时，面尾头放弃成头、变无。</param>
        internal static List<ManiaHitObject> Process(
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

                // avoid minijack：面尾产生的头若离下一个"点产生的头"小于阈值，则放弃成头、变无
                for (int i = 0; i < tokens.Count - 1; i++)
                {
                    if (tokens[i].kind != 3)
                        continue;

                    var next = tokens[i + 1];

                    if (next.kind == 1 && next.time - tokens[i].time < minijackMsAt(tokens[i].time))
                        tokens[i] = (tokens[i].time, 2, Array.Empty<HitSampleInfo>());
                }

                // 列首的"无" -> 永久点
                if (tokens.Count > 0 && tokens[0].kind == 2)
                    tokens[0] = (tokens[0].time, 0, tokens[0].samples);

                // 公共结束时间补一个"无"，保证最后一个新面头能排到尾
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
                        // 下一个是"无"：直接用它作面尾
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

    public static class TrueReleaseStrings
    {
        public static readonly LocalisableString TRUE_RELEASE_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("把相邻的点/面互相焊接，短面塌缩回点，所有列对齐到公共结束时间",
                "Weld adjacent notes into holds; collapse short holds; align all columns to a common end.");

        public static readonly LocalisableString MIN_HOLD_LENGTH_LABEL = new EzLocalizationManager.EzLocalisableString("最短面长", "Minimum hold length");
        public static readonly LocalisableString MIN_HOLD_LENGTH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("短于该长度的面会被当作点",
            "Holds shorter than this are treated as notes (unit below)");

        public static readonly LocalisableString TAIL_GAP_LABEL = new EzLocalizationManager.EzLocalisableString("面尾空隙", "Tail gap");
        public static readonly LocalisableString TAIL_GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("点被拉成面时，在与下一个对象之间留出的空隙",
            "Gap left before the next object when stretching a note into a hold (unit below)");

        public static readonly LocalisableString MINIJACK_LABEL = new EzLocalizationManager.EzLocalisableString("Minijack", "Minijack");
        public static readonly LocalisableString MINIJACK_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("面尾产生的头离下一个点产生的头小于该值时，放弃成头",
            "If a hold-tail-born head is closer than this to the next point-born head, it is dropped instead of becoming a head (unit below)");

        public static readonly LocalisableString UNIT_LABEL = new EzLocalizationManager.EzLocalisableString("单位", "Unit");
        public static readonly LocalisableString UNIT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("上述参数使用的时间单位", "Time unit used by the parameters above");
    }
}
