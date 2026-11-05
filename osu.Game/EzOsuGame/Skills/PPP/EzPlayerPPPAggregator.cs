// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
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
            Action? afterEachScore = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var bySlice = new Dictionary<int, Dictionary<string, List<double>>>();
            var analyzed = new Dictionary<int, int>();

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
                    var playable = working.GetPlayableBeatmap(score.Ruleset, mods);
                    var chart = engine.CalculateChart(playable, mods);
                    var portions = engine.CalculatePlay(chart, score);
                    int keyCount = rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID
                        ? keyCountOf(playable, score)
                        : 0;
                    int slice = EzPPPSkillCatalog.PlayerSliceKey(rulesetId, keyCount);

                    if (!bySlice.TryGetValue(slice, out var bySkill))
                    {
                        bySkill = new Dictionary<string, List<double>>(StringComparer.Ordinal);
                        bySlice[slice] = bySkill;
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

                    analyzed[slice] = analyzed.GetValueOrDefault(slice) + 1;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] PPP play failed for score {play.ScoreId}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                }
            }

            if (bySlice.Count == 0)
            {
                skillStore.WritePlayerSystemSkills(
                    username,
                    EzSkillSystems.UNSLICED_KEY,
                    EzSkillSystems.PLAYER_PPP,
                    new Dictionary<string, double>(StringComparer.Ordinal),
                    0);
                return;
            }

            foreach (var (slice, bySkill) in bySlice)
            {
                var aggregated = new Dictionary<string, double>(bySkill.Count, StringComparer.Ordinal);

                foreach (var (skillId, values) in bySkill)
                    aggregated[skillId] = EzMinaRatingAggregator.Aggregate(values);

                skillStore.WritePlayerSystemSkills(
                    username,
                    slice,
                    EzSkillSystems.PLAYER_PPP,
                    aggregated,
                    analyzed.GetValueOrDefault(slice));
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
