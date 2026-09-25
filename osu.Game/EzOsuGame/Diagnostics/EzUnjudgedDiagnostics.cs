// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 一次性落盘：本局中「没有 final 判定」的 <see cref="HitObject"/> 清单，用来定位
    /// 判定差一个、导致 completion 永不翻真的元凶。
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="EzTimingTrace.Enabled"/> 时工作。
    /// 光看 <c>judged = max - 1</c> 无法区分三种成因：物件仍活着但没被判、drawable 已被回收、
    /// 或者只向 score processor 报过 transient（<c>Poor</c> 之类）结果。
    /// 这三者对应完全不同的修法，所以必须逐物件落盘而不是只记计数。
    /// </remarks>
    public static class EzUnjudgedDiagnostics
    {
        /// <summary>
        /// 落盘当前未判定物件。返回写入的 CSV 路径；诊断关闭时返回空串。
        /// </summary>
        /// <param name="tag">文件名后缀，用于区分同一局的多次采样（如 <c>before</c> / <c>after</c>）。</param>
        public static string Capture(string tag, DrawableRuleset ruleset, ScoreProcessor scoreProcessor, IBeatmap beatmap)
        {
            if (!EzTimingTrace.Enabled || ruleset.Playfield == null)
                return string.Empty;

            Dictionary<HitObject, DrawableHitObject> drawablesByHitObject = collectDrawables(ruleset);

            // HitEvents 同时收 transient 与 final 结果，只有「在 HitEvents 里 + 无 final 判定」才是 transient-only。
            HashSet<HitObject> reportedToScoreProcessor = new HashSet<HitObject>();

            foreach (var hitEvent in scoreProcessor.HitEvents)
                reportedToScoreProcessor.Add(hitEvent.HitObject);

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Type,Column,StartTime,EndTime,MaximumJudgementOffset,Nested,ParentType,HasDrawable,DrawableJudged,DrawableAllJudged,ResultHasResult,ResultIsFinal,ResultType,ReportedToScoreProcessor");

            StringBuilder summary = new StringBuilder();
            int missing = 0;
            int aliveMissing = 0;
            int transientOnly = 0;
            int total = 0;
            int finalObjects = 0;

            foreach ((HitObject hitObject, string parentType) in enumerateHitObjects(beatmap))
            {
                total++;

                if (scoreProcessor.HasFinalResult(hitObject))
                {
                    finalObjects++;
                    continue;
                }

                missing++;

                drawablesByHitObject.TryGetValue(hitObject, out DrawableHitObject? drawable);

                if (drawable != null)
                    aliveMissing++;

                bool reported = reportedToScoreProcessor.Contains(hitObject);

                if (reported)
                    transientOnly++;

                csv.Append(EzProbeOutput.CsvEscape(hitObject.GetType().Name)).Append(',');
                csv.Append(hitObject is IHasColumn column ? column.Column.ToString() : string.Empty).Append(',');
                csv.Append(EzProbeOutput.Csv(hitObject.StartTime)).Append(',');
                csv.Append(EzProbeOutput.Csv(hitObject.GetEndTime())).Append(',');
                csv.Append(EzProbeOutput.Csv(hitObject.MaximumJudgementOffset)).Append(',');
                csv.Append(parentType.Length > 0 ? "1" : "0").Append(',');
                csv.Append(EzProbeOutput.CsvEscape(parentType)).Append(',');
                csv.Append(drawable != null ? "1" : "0").Append(',');
                csv.Append(drawable?.Judged == true ? "1" : "0").Append(',');
                csv.Append(drawable?.AllJudged == true ? "1" : "0").Append(',');
                csv.Append(drawable?.Result?.HasResult == true ? "1" : "0").Append(',');
                csv.Append(drawable?.Result?.IsFinal == true ? "1" : "0").Append(',');
                csv.Append(EzProbeOutput.CsvEscape(drawable?.Result?.Type.ToString() ?? string.Empty)).Append(',');
                csv.AppendLine(reported ? "1" : "0");

                summary.Append("  ").Append(hitObject.GetType().Name)
                       .Append(parentType.Length > 0 ? $" (nested in {parentType})" : string.Empty)
                       .Append(hitObject is IHasColumn c ? $" col={c.Column}" : string.Empty)
                       .Append($" t={hitObject.StartTime:F0}..{hitObject.GetEndTime():F0}")
                       .Append($" drawable={(drawable == null ? "recycled" : drawable.AllJudged ? "allJudged" : "alive")}")
                       .Append($" reportedToScoreProcessor={(reported ? "yes" : "no")}")
                       .AppendLine();
            }

            summary.Insert(0,
                $"[EzUnjudged.{tag}] judged={scoreProcessor.JudgedHits}/{scoreProcessor.MaximumJudgements} "
                + $"objects={total} finalObjects={finalObjects} missing={missing} aliveMissing={aliveMissing} transientOnly={transientOnly}"
                + System.Environment.NewLine);

            EzTimingTrace.Record(
                "UnjudgedDump",
                $"tag={tag} judged={scoreProcessor.JudgedHits}/{scoreProcessor.MaximumJudgements} objects={total} finalObjects={finalObjects} missing={missing} aliveMissing={aliveMissing} transientOnly={transientOnly}");

            return EzProbeOutput.WriteAsync($"unjudged_{tag}", csv.ToString(), missing, "EzUnjudged", summary.ToString());
        }

        private static Dictionary<HitObject, DrawableHitObject> collectDrawables(DrawableRuleset ruleset)
        {
            Dictionary<HitObject, DrawableHitObject> result = new Dictionary<HitObject, DrawableHitObject>();

            foreach (var drawable in ruleset.Playfield.AllHitObjects)
                collectDrawableRecursive(drawable, result);

            return result;
        }

        private static void collectDrawableRecursive(DrawableHitObject drawable, Dictionary<HitObject, DrawableHitObject> result)
        {
            if (drawable.HitObject != null)
                result[drawable.HitObject] = drawable;

            foreach (var nested in drawable.NestedHitObjects)
                collectDrawableRecursive(nested, result);
        }

        /// <summary>
        /// 按与判定相同的顺序（nested 先于本体）枚举谱面物件，并附带其父物件类型名（顶层为空）。
        /// </summary>
        private static IEnumerable<(HitObject HitObject, string ParentType)> enumerateHitObjects(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                foreach (var nested in enumerateRecursive(hitObject))
                    yield return nested;

                yield return (hitObject, string.Empty);
            }
        }

        private static IEnumerable<(HitObject HitObject, string ParentType)> enumerateRecursive(HitObject parent)
        {
            foreach (var hitObject in parent.NestedHitObjects)
            {
                foreach (var nested in enumerateRecursive(hitObject))
                    yield return nested;

                yield return (hitObject, parent.GetType().Name);
            }
        }
    }
}
