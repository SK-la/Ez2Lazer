// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Aggregates per-play PPP vectors into player skills (one weighted value per axis).
    /// Mania plays are stored on their key-count slice; other rulesets use <see cref="EzSkillSystems.UNSLICED_KEY"/>.
    /// </summary>
    public sealed class EzPlayerPPPAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly IEzPPPEngine engine;

        public EzPlayerPPPAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, IEzPPPEngine? engine = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.engine = engine ?? new EzPPPEngine();
        }

        public void ComputeAndStore(
            string username,
            IEnumerable<EzSkillPlayRow> plays,
            Func<Guid, ScoreInfo?>? resolveScore,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var bySlices = new Dictionary<(int RulesetOnlineId, int Slice), Dictionary<string, List<double>>>();
            var analyzed = new Dictionary<(int RulesetOnlineId, int Slice), int>();

            // Chart skills depend only on (chart, mods), so a second score on the same chart and mods must not
            // re-convert the beatmap or re-run the engine. Score archives repeat charts heavily.
            var chartSkills = new Dictionary<(string Hash, int ModsSignature), IReadOnlyDictionary<string, double>>();

            foreach (var play in plays)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var score = resolveScore?.Invoke(play.ScoreId);
                afterEachScore?.Invoke();

                if (score?.BeatmapInfo == null)
                    continue;

                int rulesetId = score.Ruleset.OnlineID;

                if (!EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                    continue;

                try
                {
                    var working = beatmapManager.GetWorkingBeatmap(score.BeatmapInfo);
                    var mods = score.Mods;
                    var playable = EzPlayableBeatmapCache.GetShared(working, score.Ruleset, mods, cancellationToken);

                    var chartKey = (score.BeatmapInfo.Hash, EzAnalysisLookupCache.ComputeModsSignature(mods));

                    if (!chartSkills.TryGetValue(chartKey, out var chart))
                    {
                        chart = engine.CalculateChart(playable, mods);
                        chartSkills[chartKey] = chart;
                    }

                    var portions = engine.CalculatePlay(chart, score);
                    int keyCount = rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID
                        ? keyCountOf(playable, score)
                        : 0;
                    var key = (RulesetOnlineId: rulesetId, Slice: EzPPPSkillCatalog.PlayerSliceKey(rulesetId, keyCount));

                    if (!bySlices.TryGetValue(key, out var bySkill))
                    {
                        bySkill = new Dictionary<string, List<double>>(StringComparer.Ordinal);
                        bySlices[key] = bySkill;
                    }

                    foreach (var (skillId, value) in portions)
                    {
                        if (!double.IsFinite(value) || value <= 0)
                            continue;

                        if (!bySkill.TryGetValue(skillId, out var values))
                        {
                            values = new List<double>();
                            bySkill[skillId] = values;
                        }

                        values.Add(value);
                    }

                    analyzed[key] = analyzed.GetValueOrDefault(key) + 1;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] PPP play failed for score {play.ScoreId}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                }
            }

            if (bySlices.Count == 0)
            {
                skillStore.ClearPlayerSystemSkills(username, EzSkillSystems.PLAYER_PPP);
                return;
            }

            foreach (var ((rulesetOnlineId, slice), bySkill) in bySlices)
            {
                var aggregated = new Dictionary<string, double>(bySkill.Count, StringComparer.Ordinal);

                foreach (var (skillId, values) in bySkill)
                    aggregated[skillId] = EzMinaRatingAggregator.Aggregate(values);

                skillStore.WritePlayerSystemSkills(
                    username,
                    slice,
                    EzSkillSystems.PLAYER_PPP,
                    aggregated,
                    analyzed.GetValueOrDefault((rulesetOnlineId, slice)),
                    rulesetOnlineId: rulesetOnlineId);
            }
        }

        private static int keyCountOf(IBeatmap beatmap, ScoreInfo score)
        {
            int keyCount = (int)Math.Round(score.BeatmapInfo?.Difficulty.CircleSize ?? beatmap.Difficulty.CircleSize);

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is IHasColumn column)
                    keyCount = Math.Max(keyCount, column.Column + 1);
            }

            return keyCount;
        }
    }
}
