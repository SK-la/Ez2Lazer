// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// HitEvents 调试日志工具：在 Clear 前（live）和 Session 重生成后（session）各输出一份，
    /// 用于对比分析 Session 与 Live 游玩结果的差异。
    /// </summary>
    public sealed class HitEventsDebugLog : IHitEventsDebugLogger
    {
        private static readonly string debugDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "artifacts",
            "hit_events_debug");

        private readonly ManiaGameplayEnvironment? environment;

        public HitEventsDebugLog(ManiaGameplayEnvironment? environment)
        {
            this.environment = environment;
        }

        public static void Register()
        {
            var env = ManiaRuleset.ResolveEnvironment(null, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForStoredStatistics);
            HitEventsDebugLoggerRegistry.Register(new HitEventsDebugLog(env));
        }

        static HitEventsDebugLog()
        {
            // 在 static 构造时注册，确保全局只有一个实例
        }

        public void LogLiveHitEvents(ScoreInfo score)
        {
            writeLog(score, "live");
        }

        public void LogSessionHitEvents(ScoreInfo score)
        {
            writeLog(score, "session");
        }

        private void writeLog(ScoreInfo scoreInfo, string phase)
        {
            try
            {
                Directory.CreateDirectory(debugDirectory);

                string beatmapHash = scoreInfo.BeatmapHash;
                string scoreId = scoreInfo.ID.ToString();
                string beatmapTitle = scoreInfo.BeatmapInfo?.Metadata.Title ?? "unknown";
                string safeBeatmapName = sanitizeFileName(beatmapTitle);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // 限制长度避免文件名过长
                string hashPart = beatmapHash.Length >= 8 ? beatmapHash[..8] : beatmapHash;
                string idPart = scoreId.Length >= 8 ? scoreId[..8] : scoreId;
                string baseName = $"{safeBeatmapName}_{hashPart}_{idPart}_{phase}_{timestamp}";

                writeCsv(scoreInfo, baseName);
                writeTxt(scoreInfo, phase, baseName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HitEventsDebugLog] Failed to write log: {ex.Message}");
            }
        }

        private void writeCsv(ScoreInfo scoreInfo, string baseName)
        {
            string csvPath = Path.Combine(debugDirectory, $"{baseName}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("NoteTime,NoteType,Column,Result,TimeOffset,GameplayRate,HoldDuration,HitObjectType");

            foreach (var e in scoreInfo.HitEvents)
            {
                double holdDuration = 0;
                string noteType = "Note";
                var ho = e.HitObject;

                if (ho is IHasDuration hasDuration)
                    holdDuration = hasDuration.Duration;

                if (ho is HoldNote)
                    noteType = e.LastHitObject != null ? "Tail" : "Head";
                else if (ho is TailNote)
                    noteType = "Tail";

                sb.AppendLine(string.Join(",",
                    ho.StartTime.ToString("F2"),
                    noteType,
                    (ho as IHasColumn)?.Column.ToString() ?? "N/A",
                    e.Result.ToString(),
                    e.TimeOffset.ToString("F4"),
                    e.GameplayRate?.ToString("F6") ?? "",
                    holdDuration.ToString("F2"),
                    ho.GetType().Name
                ));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"[HitEventsDebugLog] CSV written: {csvPath}");
        }

        private void writeTxt(ScoreInfo scoreInfo, string phase, string baseName)
        {
            string txtPath = Path.Combine(debugDirectory, $"{baseName}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("=== HitEvents Debug Log ===");
            sb.AppendLine($"Phase: {phase}");
            sb.AppendLine($"Beatmap: {scoreInfo.BeatmapInfo?.Metadata.Title} [{scoreInfo.BeatmapInfo?.Metadata.Source}]");
            sb.AppendLine($"BeatmapHash: {scoreInfo.BeatmapHash}");
            sb.AppendLine($"ScoreID: {scoreInfo.ID}");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine();

            if (environment != null)
            {
                sb.AppendLine("--- Environment ---");
                sb.AppendLine($"ManiaHitMode: {environment.ManiaHitMode}");
                sb.AppendLine($"HealthMode: {environment.ManiaHealthMode}");
                sb.AppendLine($"JudgePrecedence: {environment.JudgePrecedence}");
                sb.AppendLine($"OffsetPlusMania: {environment.OffsetPlusMania}");
                sb.AppendLine($"BmsPoorHitResultEnable: {environment.BmsPoorHitResultEnable}");
                sb.AppendLine();
            }

            sb.AppendLine("--- ScoreInfo ---");
            sb.AppendLine($"TotalScore: {scoreInfo.TotalScore}");
            sb.AppendLine($"Accuracy: {scoreInfo.Accuracy:F6}");
            sb.AppendLine($"MaxCombo: {scoreInfo.MaxCombo}");
            sb.AppendLine($"Rank: {scoreInfo.Rank}");
            sb.AppendLine($"Mods: {string.Join(",", scoreInfo.Mods.Select(m => m.Acronym))}");
            sb.AppendLine($"ManiaHitMode (embedded): {scoreInfo.ManiaHitMode}");
            sb.AppendLine($"ManiaHealthMode (embedded): {scoreInfo.ManiaHealthMode}");
            sb.AppendLine();

            sb.AppendLine($"--- HitEvents ({scoreInfo.HitEvents.Count} total) ---");
            for (int i = 0; i < scoreInfo.HitEvents.Count; i++)
            {
                var e = scoreInfo.HitEvents[i];
                var ho = e.HitObject;
                string extra = "";
                if (ho is IHasDuration && e.LastHitObject != null)
                    extra = $" -> tail@{e.LastHitObject.StartTime:F2}";
                if (ho is IHasColumn hasColumn)
                    extra += $" col={hasColumn.Column}";

                sb.AppendLine($"  #{i:D3}: {ho.GetType().Name}@{ho.StartTime:F2}{extra} {e.Result} offset={e.TimeOffset:F4}ms rate={e.GameplayRate:F6}");
            }
            sb.AppendLine();

            sb.AppendLine("--- Statistics ---");
            var stats = new Dictionary<HitResult, int>();
            foreach (var e in scoreInfo.HitEvents)
            {
                if (e.Result == HitResult.IgnoreHit || e.Result == HitResult.IgnoreMiss)
                    continue;
                stats[e.Result] = stats.GetValueOrDefault(e.Result, 0) + 1;
            }
            foreach (var kvp in stats.OrderBy(k => k.Key))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            sb.AppendLine();

            sb.AppendLine("--- Mods Applied ---");
            foreach (var mod in scoreInfo.Mods)
            {
                sb.AppendLine($"  {mod.GetType().Name} ({mod.Acronym})");
            }

            File.WriteAllText(txtPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"[HitEventsDebugLog] TXT written: {txtPath}");
        }

        private static string sanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "untitled";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 50 ? name[..50] : name;
        }
    }
}
