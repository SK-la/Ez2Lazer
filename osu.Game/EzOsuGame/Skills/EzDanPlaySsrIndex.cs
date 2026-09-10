// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Hub <c>play.values</c> lookup for dan skillset filing: rebuild Mina SSR vectors from
    /// <see cref="EzAxisPlayEvidenceRow"/> (written by <see cref="EzPlayerSsrAggregator"/>).
    /// </summary>
    public sealed class EzDanPlaySsrIndex
    {
        private readonly List<PlaySnapshot> plays;

        private EzDanPlaySsrIndex(List<PlaySnapshot> plays)
        {
            this.plays = plays;
        }

        public static EzDanPlaySsrIndex FromAxisPlays(IReadOnlyList<EzAxisPlayEvidenceRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var grouped = new Dictionary<PlayKey, Dictionary<string, double>>();

            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.BeatmapHash))
                    continue;

                // Pattern ratings are not Mina play.values.
                if (row.SkillId.StartsWith(EzSkillSystems.PLAYER_PATTERN + ".", StringComparison.Ordinal))
                    continue;

                if (!row.SkillId.StartsWith(EzSkillSystems.PLAYER_SSR + ".", StringComparison.Ordinal)
                    && !EzMinaSkillAxisExtensions.TryParse(row.SkillId, out _))
                    continue;

                var key = new PlayKey(row.BeatmapHash, row.KeyCount, roundRate(row.Rate), row.ScoredAt.ToUnixTimeMilliseconds());

                if (!grouped.TryGetValue(key, out var values))
                    grouped[key] = values = new Dictionary<string, double>(StringComparer.Ordinal);

                values[row.SkillId] = row.AxisValue;

                // Keep accuracy on a side channel via Overall row match — stored on snapshot below.
            }

            // Second pass: attach accuracy from any row in the same play group.
            var accuracyByKey = new Dictionary<PlayKey, double>();

            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.BeatmapHash))
                    continue;

                var key = new PlayKey(row.BeatmapHash, row.KeyCount, roundRate(row.Rate), row.ScoredAt.ToUnixTimeMilliseconds());
                if (!grouped.ContainsKey(key))
                    continue;

                accuracyByKey[key] = row.Accuracy;
            }

            var list = new List<PlaySnapshot>(grouped.Count);

            foreach ((PlayKey key, Dictionary<string, double> raw) in grouped)
            {
                var mina = EzDanSkillsetFiling.NormalizeMsdValues(raw);
                if (mina.Count == 0)
                    continue;

                accuracyByKey.TryGetValue(key, out double accuracy);
                list.Add(new PlaySnapshot(key.Hash, key.KeyCount, key.Rate, key.ScoredAtMs, accuracy, mina));
            }

            return new EzDanPlaySsrIndex(list);
        }

        /// <summary>Hub: the rated play's SSR vector for this clear; null when no matching axis evidence.</summary>
        public IReadOnlyDictionary<string, double>? Resolve(EzDanClearEvidenceRow clear)
        {
            if (string.IsNullOrEmpty(clear.BeatmapHash) || plays.Count == 0)
                return null;

            double rate = roundRate(clear.Rate);
            long scoredAtMs = clear.ScoredAt.ToUnixTimeMilliseconds();

            PlaySnapshot? best = null;
            long bestTimeDelta = long.MaxValue;
            double bestAccDelta = double.MaxValue;

            foreach (var play in plays)
            {
                if (play.KeyCount != clear.KeyCount)
                    continue;

                if (!string.Equals(play.Hash, clear.BeatmapHash, StringComparison.Ordinal))
                    continue;

                if (Math.Abs(play.Rate - rate) > 0.0005)
                    continue;

                long timeDelta = Math.Abs(play.ScoredAtMs - scoredAtMs);
                double accDelta = Math.Abs(play.Accuracy - clear.Accuracy);

                if (best == null
                    || timeDelta < bestTimeDelta
                    || (timeDelta == bestTimeDelta && accDelta < bestAccDelta))
                {
                    best = play;
                    bestTimeDelta = timeDelta;
                    bestAccDelta = accDelta;
                }
            }

            return best?.Values;
        }

        private static double roundRate(double rate)
            => Math.Round(rate, 4, MidpointRounding.AwayFromZero);

        private readonly record struct PlayKey(string Hash, int KeyCount, double Rate, long ScoredAtMs);

        private sealed class PlaySnapshot
        {
            public PlaySnapshot(string hash, int keyCount, double rate, long scoredAtMs, double accuracy, IReadOnlyDictionary<string, double> values)
            {
                Hash = hash;
                KeyCount = keyCount;
                Rate = rate;
                ScoredAtMs = scoredAtMs;
                Accuracy = accuracy;
                Values = values;
            }

            public string Hash { get; }
            public int KeyCount { get; }
            public double Rate { get; }
            public long ScoredAtMs { get; }
            public double Accuracy { get; }
            public IReadOnlyDictionary<string, double> Values { get; }
        }
    }
}
