// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using osu.Framework.Platform;
using osu.Game.Scoring.Legacy;

namespace osu.Game.EzOsuGame.Database
{
    /// <summary>
    /// Fast-path stamp so BDSP can skip full score-upgrade table scans on clean startups.
    /// Stored as a small file under EzData (no Realm schema bump).
    /// </summary>
    public static class EzScoreScanStamp
    {
        public const int MOD_MULTIPLIER_GATE_VERSION = 30000017;
        public const int RANK_GATE_VERSION = 30000013;

        private const string relative_path = "EzData/score-scan-stamp.txt";
        private const string format_version = "1";

        public readonly record struct Snapshot(int ScoreCount, int FailedCount);

        public static void Invalidate(Storage storage)
        {
            try
            {
                string path = storage.GetFullPath(relative_path, createIfNotExisting: false);

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort: a stale stamp only causes an extra scan next launch.
            }
        }

        public static bool Matches(Storage storage, Snapshot live)
        {
            try
            {
                string path = storage.GetFullPath(relative_path, createIfNotExisting: false);

                if (!File.Exists(path))
                    return false;

                string[] lines = File.ReadAllLines(path);

                if (lines.Length < 7 || lines[0] != format_version)
                    return false;

                if (!tryReadInt(lines, "encoder", out int encoder)
                    || encoder != LegacyScoreEncoder.LATEST_VERSION)
                    return false;

                if (!tryReadInt(lines, "mod_gate", out int modGate)
                    || modGate != MOD_MULTIPLIER_GATE_VERSION)
                    return false;

                if (!tryReadInt(lines, "rank_gate", out int rankGate)
                    || rankGate != RANK_GATE_VERSION)
                    return false;

                if (!tryReadInt(lines, "score_count", out int scoreCount)
                    || scoreCount != live.ScoreCount)
                    return false;

                if (!tryReadInt(lines, "failed_count", out int failedCount)
                    || failedCount != live.FailedCount)
                    return false;

                if (!tryReadInt(lines, "stats_pass_done", out int statsPassDone)
                    || statsPassDone != 1)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Write(Storage storage, Snapshot live)
        {
            try
            {
                string path = storage.GetFullPath(relative_path, createIfNotExisting: true);
                string? directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllLines(path, new[]
                {
                    format_version,
                    $"encoder={LegacyScoreEncoder.LATEST_VERSION.ToString(CultureInfo.InvariantCulture)}",
                    $"mod_gate={MOD_MULTIPLIER_GATE_VERSION.ToString(CultureInfo.InvariantCulture)}",
                    $"rank_gate={RANK_GATE_VERSION.ToString(CultureInfo.InvariantCulture)}",
                    $"score_count={live.ScoreCount.ToString(CultureInfo.InvariantCulture)}",
                    $"failed_count={live.FailedCount.ToString(CultureInfo.InvariantCulture)}",
                    "stats_pass_done=1",
                });
            }
            catch
            {
                // Non-fatal: next startup will rescan.
            }
        }

        private static bool tryReadInt(string[] lines, string key, out int value)
        {
            value = 0;
            string prefix = key + "=";

            foreach (string line in lines)
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                return int.TryParse(line.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }
    }
}
