// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 通用分析属性。
    /// 当前仅包含所有规则集都可消费的 KPS 相关数据。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EzCommonAnalysisAttributes
    {
        [JsonProperty("kps_avg", Order = -5)]
        public double AverageKps { get; set; }

        [JsonProperty("kps_max", Order = -4)]
        public double MaxKps { get; set; }

        [JsonProperty("kps_list", Order = -3)]
        public IReadOnlyList<double> KpsList { get; set; } = Array.Empty<double>();

        public static EzCommonAnalysisAttributes Create(double averageKps, double maxKps, IReadOnlyList<double> kpsList)
            => new EzCommonAnalysisAttributes
            {
                AverageKps = averageKps,
                MaxKps = maxKps,
                KpsList = kpsList,
            };
    }

    /// <summary>
    /// 规则集专属分析属性的基类。
    /// </summary>
    public abstract class EzRulesetAnalysisAttributes
    {
    }

    /// <summary>
    /// mania 专属分析属性。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EzManiaAnalysisAttributes : EzRulesetAnalysisAttributes
    {
        [JsonProperty("xxy_sr", Order = -2)]
        public double? XxySr { get; set; }

        // TODO: 仅预留字段。当前阶段不参与计算、不参与持久化版本升级，也不接入 UI / 本地化。
        [JsonIgnore]
        public double? XxySrFullLN4 { get; set; }

        [JsonIgnore]
        public double? XxySrFullLN8 { get; set; }

        [JsonProperty("column_counts", Order = -1)]
        public Dictionary<int, int> ColumnCounts { get; set; } = new Dictionary<int, int>();

        [JsonProperty("hold_note_counts", Order = 0)]
        public Dictionary<int, int> HoldNoteCounts { get; set; } = new Dictionary<int, int>();

        [JsonIgnore]
        public bool HasManiaData => ColumnCounts.Count > 0 || HoldNoteCounts.Count > 0 || XxySr.HasValue || XxySrFullLN4.HasValue || XxySrFullLN8.HasValue;

        public static EzManiaAnalysisAttributes Create(Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts, double? xxySr,
                                                       double? xxySrFullLN4 = null, double? xxySrFullLN8 = null)
            => new EzManiaAnalysisAttributes
            {
                ColumnCounts = columnCounts,
                HoldNoteCounts = holdNoteCounts,
                XxySr = xxySr,
                XxySrFullLN4 = xxySrFullLN4,
                XxySrFullLN8 = xxySrFullLN8,
            };
    }

    public readonly record struct EzAnalysisResult
    {
        public readonly double AverageKps;
        public readonly double MaxKps;
        public readonly EzCommonAnalysisAttributes? CommonAttributes;
        public readonly EzRulesetAnalysisAttributes? RulesetAttributes;

        public EzAnalysisResult(EzCommonAnalysisAttributes commonAttributes, EzRulesetAnalysisAttributes? rulesetAttributes = null)
        {
            AverageKps = double.IsFinite(commonAttributes.AverageKps) ? commonAttributes.AverageKps : 0;
            MaxKps = double.IsFinite(commonAttributes.MaxKps) ? commonAttributes.MaxKps : 0;
            CommonAttributes = commonAttributes;
            RulesetAttributes = rulesetAttributes;
        }

        [JsonIgnore]
        public EzManiaAnalysisAttributes? ManiaAttributes => RulesetAttributes as EzManiaAnalysisAttributes;

        [JsonIgnore]
        public IReadOnlyList<double> KpsList => CommonAttributes?.KpsList ?? Array.Empty<double>();
    }

    /// <summary>
    /// 更详细的 mania 统计信息：列统计、长按统计、scratch 描述与可选的 xxy_sr。
    /// 其中 FullLN 字段当前仅做结果模型预留，不参与现阶段功能实现。
    /// </summary>
    public readonly struct EzManiaSummary
    {
        public static readonly EzManiaSummary EMPTY = new EzManiaSummary(new Dictionary<int, int>(), new Dictionary<int, int>(), null);

        public readonly Dictionary<int, int> ColumnCounts;
        public readonly Dictionary<int, int> HoldNoteCounts;
        public readonly double? XxySr;
        public readonly double? XxySrFullLN4;
        public readonly double? XxySrFullLN8;

        public EzManiaSummary(Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts, double? xxySr, double? xxySrFullLN4 = null, double? xxySrFullLN8 = null)
        {
            ColumnCounts = columnCounts;
            HoldNoteCounts = holdNoteCounts;
            XxySr = xxySr;
            XxySrFullLN4 = xxySrFullLN4;
            XxySrFullLN8 = xxySrFullLN8;
        }
    }
}
