// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    ///     Pure port of mania-hub <c>player-skills.ts</c> dan skillset filing
    ///     (<c>bucketingSkillset</c> / <c>bucketsForClear</c> / <c>groupDanClearsBySkillset</c> primary quorum).
    ///     MSD keys are Mina names (<c>Stream</c>, <c>JackSpeed</c>, …); <see cref="NormalizeMsdValues"/>
    ///     also accepts <c>beatmap_msd.*</c> ids from <see cref="EzSkillProvider.GetBeatmapMsd"/>.
    /// </summary>
    public static class EzDanSkillsetFiling
    {
        // --- Pattern / cluster tag floors (hub) ---
        public const double PATTERN_TAG_MIN_SCORE = 0.5;
        public const double CHORDJACK_TAG_MIN_SCORE = 0.8;
        public const double DELAY_TAG_MIN_SCORE = 0.25;
        public const double CLUSTER_SHARE_MIN = 0.4;
        public const double JACK_TECH_VETO_MIN_SCORE = 0.8;

        public const double TRILL_JACK_MIN_CHORDJACK = 0.60;
        public const double TRILL_JACK_CORROBORATED_CHORDJACK = 0.55;
        public const double TRILL_JACK_CORROBORATED_SHARE = 0.15;
        public const double TRILL_RUNNER_UP_MIN_LENGTH_SECONDS = 240;

        public const double STAMINA_TILE_JACK_VETO_SHARE = 0.30;
        public const double STAMINA_TILE_MIN_LENGTH_SECONDS = 240;
        public const double STAMINA_HOLD_BASE_BAND = 0.5;

        public const double SPEED_NEAR_TIE_MSD = 1.25;
        public const double TECH_NEAR_TIE_MIN_SCORE = 0.8;
        public const double TECH_NEAR_TIE_MSD_LEAD = 0.35;
        public const double HANDSTREAM_NEAR_TIE_MSD = 0.95;

        public const double SPEED_TECH_DUAL_LOW = 0.35;
        public const double SPEED_TECH_DUAL_HIGH = 0.75;

        public static readonly string[] SKILL_RATING_SKILLSETS =
        {
            "Overall",
            "Stream",
            "Jumpstream",
            "Handstream",
            "Stamina",
            "JackSpeed",
            "Chordjack",
            "Technical",
        };

        private static readonly string[] rice_msd_skillsets =
            SKILL_RATING_SKILLSETS.Where(s => s is not ("Overall" or "Stamina" or "Handstream")).ToArray();

        private static readonly string[] base_msd_skillsets =
            SKILL_RATING_SKILLSETS.Where(s => s is not ("Overall" or "Stamina")).ToArray();

        private static readonly string[] stamina_hold_rivals = { "Technical", "Jumpstream", "Handstream" };

        private static readonly string[] inverse_mod_patterns = { "ln", "lninverse" };

        private static readonly HashSet<int> pattern_axis_key_counts = new HashSet<int> { 6, 7, 8 };

        private static readonly (double Mean, double Sd, double Weight)[] speed_tech_terms =
        {
            (0.0439, 0.0659, 0.7207), // rhythmBreak
            (0.0551, 0.0456, 0.9009), // crossHandTrill
            (0.0044, 0.0065, 0.5595), // miniJack
            (0.2300, 0.0534, 0.2510), // sameHand
            (-0.3886, 0.7180, 0.1252), // Technical - Stream
            (0.3927, 0.1981, 1.2100), // analyzer tech score
        };

        private const double SPEED_TECH_BIAS = -0.5869;

        /// <summary>Ordered primary+shared bucket ids for one play (hub <c>danSkillsetBucketsForValues</c>).</summary>
        public static IReadOnlyList<string> BucketsForValues(
            int keyCount,
            EzDanSide side,
            IReadOnlyDictionary<string, double> msdValues,
            double? lengthSeconds,
            double rate,
            EzChartSkillInfo? chart)
        {
            var values = NormalizeMsdValues(msdValues);
            var buckets = bucketsFor(keyCount, side);
            string? top = BucketingSkillset(
                values,
                lengthSeconds ?? chart?.LengthSeconds,
                rate,
                chart?.TechScore ?? 0,
                chart?.JackShare,
                chart?.HandstreamCluster == true);

            return BucketsForClear(buckets, top, chart, values, rate)
                   .Select(b => b.Id)
                   .ToList();
        }

        /// <summary>
        ///     Group clears into skillset verdicts. A tile opens on primary-filing quorum only;
        ///     averages over all filings into that tile (shared clears included).
        /// </summary>
        public static IReadOnlyDictionary<string, EzDanSkillsetVerdict> ComputeVerdicts(
            int keyCount,
            EzDanSide side,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, IReadOnlyDictionary<string, double>?> resolveMsd,
            Func<string, EzChartSkillInfo?> resolveChart)
        {
            var result = new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal);
            var bucketDefs = bucketsFor(keyCount, side);

            if (bucketDefs.Count == 0 || clears.Count == 0)
                return result;

            var byBucket = new Dictionary<string, List<double>>(StringComparer.Ordinal);
            var primaryByBucket = new Dictionary<string, List<double>>(StringComparer.Ordinal);

            foreach (var bucket in bucketDefs)
            {
                byBucket[bucket.Id] = new List<double>();
                primaryByBucket[bucket.Id] = new List<double>();
            }

            foreach (var clear in clears)
            {
                if (clear.KeyCount != keyCount)
                    continue;
                if (string.IsNullOrEmpty(clear.BeatmapHash))
                    continue;

                var chart = resolveChart(clear.BeatmapHash);

                if (chart != null)
                {
                    if (chart.Vibro || !chart.DanEligible)
                        continue;

                    if (chart.LnRatio is double lnRatio)
                    {
                        var chartSide = lnRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount)
                            ? EzDanSide.Ln
                            : EzDanSide.Rc;
                        if (chartSide != side)
                            continue;
                    }
                    else if (!sideMatches(clear.Side, side))
                    {
                        continue;
                    }
                }
                else if (!sideMatches(clear.Side, side))
                {
                    continue;
                }

                var rawMsd = resolveMsd(clear.BeatmapHash);
                var values = NormalizeMsdValues(rawMsd ?? new Dictionary<string, double>());

                double? length = chart?.LengthSeconds;
                string? top = BucketingSkillset(
                    values,
                    length,
                    clear.Rate,
                    chart?.TechScore ?? 0,
                    chart?.JackShare,
                    chart?.HandstreamCluster == true);

                var filed = BucketsForClear(bucketDefs, top, chart, values, clear.Rate);

                for (int index = 0; index < filed.Count; index++)
                {
                    var bucket = filed[index];
                    byBucket[bucket.Id].Add(clear.CreditedDan);

                    // Tag keymodes overlap by analyzer tag; every tag filing is primary.
                    // Only 4K shared tiles rank (skillsets != null).
                    if (index == 0 || bucket.Skillsets == null)
                        primaryByBucket[bucket.Id].Add(clear.CreditedDan);
                }
            }

            var ladder = EzDanLadders.For(keyCount, side);

            foreach (var bucket in bucketDefs)
            {
                if (primaryByBucket[bucket.Id].Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var all = byBucket[bucket.Id];
                if (all.Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var window = all
                             .OrderByDescending(v => v)
                             .Take(EzDanAlgorithm.CLEAR_WINDOW)
                             .ToList();

                double rawDan = window.Average();
                result[bucket.Id] = new EzDanSkillsetVerdict(bucket.Id, rawDan, ladder.ParseLabel(rawDan), all.Count);
            }

            return result;
        }

        /// <summary>
        ///     Normalize MSD dict keys to Mina skillset names used by filing.
        ///     Accepts <c>beatmap_msd.stream</c>, bare <c>stream</c>/<c>jack</c>/<c>tech</c>, and Mina <c>Stream</c>/<c>JackSpeed</c>.
        /// </summary>
        public static IReadOnlyDictionary<string, double> NormalizeMsdValues(IReadOnlyDictionary<string, double>? source)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            if (source == null || source.Count == 0)
                return result;

            foreach ((string key, double value) in source)
            {
                if (!double.IsFinite(value) || value <= 0)
                    continue;

                string? mina = toMinaSkillsetName(key);
                if (mina == null)
                    continue;

                if (!result.TryGetValue(mina, out double existing) || value > existing)
                    result[mina] = value;
            }

            return result;
        }

        public static bool UsesPatternSkillAxes(int keyCount) => pattern_axis_key_counts.Contains(keyCount);

        public static double PatternTagMinScore(string patternId)
        {
            if (patternId == "chordjack")
                return CHORDJACK_TAG_MIN_SCORE;
            if (patternId == "delay")
                return DELAY_TAG_MIN_SCORE;

            return PATTERN_TAG_MIN_SCORE;
        }

        public static bool ChartIsJack(int? keyCount, double chordjackScore, double jackScore, double? jackShare)
        {
            if (keyCount == null || !UsesPatternSkillAxes(keyCount.Value))
                return chordjackScore >= CHORDJACK_TAG_MIN_SCORE;

            if (jackScore >= PATTERN_TAG_MIN_SCORE)
                return true;

            return jackShare != null
                ? jackShare >= CLUSTER_SHARE_MIN
                : chordjackScore >= CHORDJACK_TAG_MIN_SCORE;
        }

        public static bool JackVetoesTech(int? keyCount, double chordjackScore, double jackScore, double? jackShare)
        {
            if (keyCount == null || !UsesPatternSkillAxes(keyCount.Value))
                return chordjackScore >= CHORDJACK_TAG_MIN_SCORE;

            if (jackScore >= JACK_TECH_VETO_MIN_SCORE)
                return true;

            return jackShare != null
                ? jackShare >= CLUSTER_SHARE_MIN
                : chordjackScore >= CHORDJACK_TAG_MIN_SCORE;
        }

        public static bool TrillIsJack(EzChartSkillInfo? chart)
        {
            if (chart?.ClusterTrill != true)
                return false;

            if (chart.ChordjackScore >= TRILL_JACK_MIN_CHORDJACK)
                return true;

            return chart.ChordjackScore >= TRILL_JACK_CORROBORATED_CHORDJACK
                   && (chart.JackShare ?? 0) >= TRILL_JACK_CORROBORATED_SHARE;
        }

        public static bool ChartBelongsToTagBucket(EzDanSkillsetBucketDef bucket, EzChartSkillInfo? chart)
        {
            if (bucket.ClusterFamily == "tech")
            {
                if (chart?.TechCategory != null)
                    return chart.TechCategory.Value;
            }
            else if (bucket.ClusterFamily != null)
            {
                double? share = bucket.ClusterFamily == "jack" ? chart?.JackShare : chart?.StreamShare;
                if (share != null)
                    return share >= CLUSTER_SHARE_MIN;
            }

            string[] patterns = chart?.Patterns ?? [];
            return patterns.Any(tag => bucket.Tags.Contains(tag, StringComparer.Ordinal));
        }

        public static string? DominantSkillset(IReadOnlyDictionary<string, double>? values)
        {
            string? best = null;
            double bestValue = 0;

            foreach (string skillset in SKILL_RATING_SKILLSETS)
            {
                if (skillset == "Overall")
                    continue;

                double value = values != null && values.TryGetValue(skillset, out double v) ? v : 0;

                if (double.IsFinite(value) && value > bestValue)
                {
                    best = skillset;
                    bestValue = value;
                }
            }

            return best;
        }

        public static double? EnduranceSeconds(double? lengthSeconds, double rate)
        {
            if (lengthSeconds == null)
                return null;

            double safeRate = double.IsFinite(rate) && rate > 0 ? rate : 1;
            double played = lengthSeconds.Value / safeRate;
            return Math.Max(lengthSeconds.Value, played);
        }

        public static EzChartSkillInfo? InverseModChartInfo(EzChartSkillInfo? chart)
        {
            if (chart == null)
                return null;

            return new EzChartSkillInfo
            {
                Patterns = inverse_mod_patterns,
                JackDemand = false,
                JackShare = null,
                StreamShare = null,
                TechCategory = null,
                ClusterTrill = null,
                HandstreamCluster = null,
                HandstreamEndurance = chart.HandstreamEndurance,
                TechScore = 0,
                ChordjackScore = 0,
                Motion = chart.Motion,
                LnRatio = chart.LnRatio,
                Vibro = chart.Vibro,
                DanEligible = chart.DanEligible,
                LengthSeconds = chart.LengthSeconds,
                KeyCount = chart.KeyCount,
            };
        }

        public static string JumpstreamRunnerUp(IReadOnlyDictionary<string, double>? values, bool contaminated)
        {
            var pool = SKILL_RATING_SKILLSETS
                       .Where(skillset => skillset != "Overall"
                                          && skillset != "Jumpstream"
                                          && !(contaminated && skillset is "Stamina" or "Handstream"))
                       .ToArray();

            return DominantSkillset(pickSkillsets(values, pool)) ?? "Jumpstream";
        }

        public static (string Primary, bool Shared)? SpeedTechTiles(
            IReadOnlyDictionary<string, double>? values,
            EzMotionFeatures? motion,
            double techScore)
        {
            double? probability = speedTechProbability(values, motion, techScore);
            if (probability == null)
                return null;

            return (
                probability >= 0.5 ? "tech" : "speed",
                probability > SPEED_TECH_DUAL_LOW && probability < SPEED_TECH_DUAL_HIGH
            );
        }

        public static string? BucketingSkillset(
            IReadOnlyDictionary<string, double>? values,
            double? lengthSeconds = null,
            double rate = 1,
            double chartTechScore = 0,
            double? chartJackShare = null,
            bool chartHandstreamCluster = false)
        {
            string? top = DominantSkillset(values);
            if (top == null)
                return top;

            if (hasHandstreamEndurance(values) && !jackContaminated(chartJackShare))
                return "Handstream";

            double stream = get(values, "Stream");
            double? endurance = EnduranceSeconds(lengthSeconds, rate);
            bool demandsEndurance = endurance >= STAMINA_TILE_MIN_LENGTH_SECONDS;

            if (top == "Stamina" && demandsEndurance
                && staminaHoldRival(values) >= stream - STAMINA_HOLD_BASE_BAND
                && !jackContaminated(chartJackShare))
            {
                return top;
            }

            double best = get(values, top);

            if (top != "Handstream" && chartHandstreamCluster && !jackContaminated(chartJackShare))
            {
                double handstream = get(values, "Handstream");
                if (handstream > 0 && handstream >= best - HANDSTREAM_NEAR_TIE_MSD)
                    return "Handstream";
            }

            string nearTie = top == "Stream" || (stream > 0 && stream >= best - SPEED_NEAR_TIE_MSD)
                ? "Stream"
                : top;

            if (nearTie == "Stream")
            {
                double technical = get(values, "Technical");
                bool techBacked = chartTechScore >= TECH_NEAR_TIE_MIN_SCORE
                                  && technical > 0
                                  && technical >= stream - SPEED_NEAR_TIE_MSD;
                bool leadBacked = chartTechScore >= PATTERN_TAG_MIN_SCORE
                                  && technical > 0
                                  && technical - stream >= TECH_NEAR_TIE_MSD_LEAD;
                return techBacked || leadBacked ? "Technical" : "Stream";
            }

            if (nearTie == "Handstream" && jackContaminated(chartJackShare))
            {
                return BucketingSkillset(
                    pickSkillsets(values, rice_msd_skillsets),
                    null,
                    1,
                    chartTechScore,
                    chartJackShare,
                    chartHandstreamCluster);
            }

            if (nearTie != "Stamina" || lengthSeconds == null)
                return nearTie;

            if (demandsEndurance && !jackContaminated(chartJackShare))
                return nearTie;

            return BucketingSkillset(
                pickSkillsets(values, base_msd_skillsets),
                null,
                1,
                chartTechScore,
                chartJackShare,
                chartHandstreamCluster);
        }

        public static IReadOnlyList<EzDanSkillsetBucketDef> BucketsForClear(
            IReadOnlyList<EzDanSkillsetBucketDef> buckets,
            string? topSkillset,
            EzChartSkillInfo? chart,
            IReadOnlyDictionary<string, double>? values,
            double rate = 1)
        {
            if (chart?.JackDemand == true || TrillIsJack(chart))
            {
                var jack = buckets.FirstOrDefault(b => b.Id == EzDanSkillsetBuckets.JACK && b.Skillsets != null);
                if (jack != null)
                    return ResolveTilesForClear(new[] { jack }, buckets, chart, values);
            }

            var overrideBucket = buckets.FirstOrDefault(b =>
                b.Skillsets != null && b.Tags.Length > 0 && ChartBelongsToTagBucket(b, chart));
            if (overrideBucket != null)
                return ResolveTilesForClear(new[] { overrideBucket }, buckets, chart, values);

            if (chart is { HandstreamEndurance: true, TechCategory: false, ClusterTrill: false }
                && !jackContaminated(chart.JackShare))
            {
                var stamina = buckets.FirstOrDefault(b => b.Id == EzDanSkillsetBuckets.STAMINA && b.Skillsets != null);
                if (stamina != null)
                    return new[] { stamina };
            }

            bool contaminated = jackContaminated(chart?.JackShare);
            string? effectiveTop;

            if (topSkillset != "Jumpstream" || chart?.ClusterTrill == null)
            {
                effectiveTop = topSkillset;
            }
            else if (chart.ClusterTrill == true)
            {
                effectiveTop = (EnduranceSeconds(chart.LengthSeconds, rate) ?? 0) >= TRILL_RUNNER_UP_MIN_LENGTH_SECONDS
                    ? JumpstreamRunnerUp(values, contaminated)
                    : topSkillset;
            }
            else if (chart.TechCategory == true)
            {
                effectiveTop = JumpstreamRunnerUp(values, contaminated);
            }
            else
            {
                effectiveTop = contaminated ? topSkillset : "Stamina";
            }

            var filed = buckets
                        .Where(bucket => bucket.Skillsets != null
                            ? effectiveTop != null && bucket.Skillsets.Contains(effectiveTop, StringComparer.Ordinal)
                            : ChartBelongsToTagBucket(bucket, chart))
                        .ToList();

            return ResolveTilesForClear(filed, buckets, chart, values);
        }

        public static IReadOnlyList<EzDanSkillsetBucketDef> ResolveTilesForClear(
            IReadOnlyList<EzDanSkillsetBucketDef> filed,
            IReadOnlyList<EzDanSkillsetBucketDef> buckets,
            EzChartSkillInfo? chart,
            IReadOnlyDictionary<string, double>? values)
        {
            if (filed.Count != 1 || filed[0].Skillsets == null)
                return filed;

            var primary = filed[0];

            IReadOnlyList<EzDanSkillsetBucketDef> add(string id)
            {
                var sibling = buckets.FirstOrDefault(b => b.Id == id && b.Skillsets != null);
                return sibling != null ? new[] { primary, sibling } : filed;
            }

            if (primary.Id is EzDanSkillsetBuckets.TECH or EzDanSkillsetBuckets.SPEED)
            {
                var modelled = SpeedTechTiles(values, chart?.Motion, chart?.TechScore ?? 0);
                if (modelled == null)
                    return filed;

                var decided = buckets.FirstOrDefault(b => b.Id == modelled.Value.Primary && b.Skillsets != null);
                if (decided == null)
                    return filed;

                if (!modelled.Value.Shared)
                    return new[] { decided };

                string otherId = modelled.Value.Primary == "tech" ? "speed" : "tech";
                var other = buckets.FirstOrDefault(b => b.Id == otherId && b.Skillsets != null);
                return other != null ? new[] { decided, other } : new[] { decided };
            }

            if (primary.Id == EzDanSkillsetBuckets.STAMINA)
            {
                if (DominantSkillset(values) != "Stamina")
                    return filed;

                string? baseSkill = DominantSkillset(pickSkillsets(values, base_msd_skillsets));
                if (baseSkill is not ("Stream" or "Technical"))
                    return filed;

                var modelled = SpeedTechTiles(values, chart?.Motion, chart?.TechScore ?? 0);
                if (modelled == null || modelled.Value.Primary != "tech")
                    return filed;

                return add("tech");
            }

            return filed;
        }

        public static IReadOnlyList<EzDanSkillsetBucketDef> Buckets(int keyCount, EzDanSide side)
            => bucketsFor(keyCount, side);

        private static IReadOnlyList<EzDanSkillsetBucketDef> bucketsFor(int keyCount, EzDanSide side)
        {
            if (side == EzDanSide.Ln)
            {
                if (keyCount == 7)
                {
                    return new[]
                    {
                        new EzDanSkillsetBucketDef("lngeneral", new[] { "lngeneral", "ln" }, anchor: true),
                        new EzDanSkillsetBucketDef("lntech", new[] { "lntech" }),
                        new EzDanSkillsetBucketDef("lninverse", new[] { "lninverse" }),
                        new EzDanSkillsetBucketDef("lnrelease", new[] { "lnrelease" }),
                    };
                }

                return Array.Empty<EzDanSkillsetBucketDef>();
            }

            if (keyCount == 4)
            {
                return new[]
                {
                    new EzDanSkillsetBucketDef("jack", new[] { "chordjack", "speedjack" },
                        skillsets: new[] { "JackSpeed", "Chordjack" }),
                    new EzDanSkillsetBucketDef("tech", Array.Empty<string>(),
                        skillsets: new[] { "Technical", "Jumpstream" }),
                    new EzDanSkillsetBucketDef("speed", Array.Empty<string>(),
                        skillsets: new[] { "Stream" }),
                    new EzDanSkillsetBucketDef("stamina", Array.Empty<string>(),
                        skillsets: new[] { "Handstream", "Stamina" }),
                };
            }

            return new[]
            {
                new EzDanSkillsetBucketDef("jack", new[] { "jack" }, clusterFamily: "jack"),
                new EzDanSkillsetBucketDef("tech", new[] { "tech" }, clusterFamily: "tech"),
                new EzDanSkillsetBucketDef("speed", new[] { "delay" }),
                new EzDanSkillsetBucketDef("stream", new[] { "chordstream", "bracket" }, clusterFamily: "stream"),
            };
        }

        private static double? speedTechProbability(
            IReadOnlyDictionary<string, double>? values,
            EzMotionFeatures? motion,
            double techScore)
        {
            if (motion == null)
                return null;

            double[] inputs =
            {
                motion.RhythmBreak,
                motion.CrossHandTrill,
                motion.MiniJack,
                motion.SameHand,
                get(values, "Technical") - get(values, "Stream"),
                techScore,
            };

            double z = SPEED_TECH_BIAS;

            for (int i = 0; i < inputs.Length; i++)
            {
                double input = inputs[i];
                if (!double.IsFinite(input))
                    return null;

                var term = speed_tech_terms[i];
                z += term.Weight * ((input - term.Mean) / term.Sd);
            }

            return 1 / (1 + Math.Exp(-z));
        }

        private static bool hasHandstreamEndurance(IReadOnlyDictionary<string, double>? values)
            => DominantSkillset(values) == "Stamina"
               && DominantSkillset(pickSkillsets(values, base_msd_skillsets)) == "Handstream";

        private static bool jackContaminated(double? jackShare)
            => jackShare >= STAMINA_TILE_JACK_VETO_SHARE;

        private static double staminaHoldRival(IReadOnlyDictionary<string, double>? values)
            => stamina_hold_rivals.Max(skillset => get(values, skillset));

        private static IReadOnlyDictionary<string, double> pickSkillsets(
            IReadOnlyDictionary<string, double>? values,
            IReadOnlyList<string> keep)
        {
            var picked = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (string skillset in keep)
            {
                double value = get(values, skillset);
                if (double.IsFinite(value) && value > 0)
                    picked[skillset] = value;
            }

            return picked;
        }

        private static double get(IReadOnlyDictionary<string, double>? values, string skillset)
            => values != null && values.TryGetValue(skillset, out double v) && double.IsFinite(v) ? v : 0;

        private static bool sideMatches(string clearSide, EzDanSide side)
            => string.Equals(clearSide, side.ToId(), StringComparison.OrdinalIgnoreCase);

        private static string? toMinaSkillsetName(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            // Already a Mina name.
            foreach (string name in SKILL_RATING_SKILLSETS)
            {
                if (string.Equals(key, name, StringComparison.Ordinal))
                    return name;
            }

            string bare = key;
            int dot = key.LastIndexOf('.');
            if (dot >= 0 && dot < key.Length - 1)
                bare = key[(dot + 1)..];

            // Legacy MSD ids.
            if (string.Equals(bare, "jack_speed", StringComparison.OrdinalIgnoreCase))
                return "JackSpeed";
            if (string.Equals(bare, "technical", StringComparison.OrdinalIgnoreCase))
                return "Technical";

            if (EzMinaSkillAxisExtensions.TryParse(bare, out var axis))
            {
                return axis switch
                {
                    EzMinaSkillAxis.Overall => "Overall",
                    EzMinaSkillAxis.Stream => "Stream",
                    EzMinaSkillAxis.Jumpstream => "Jumpstream",
                    EzMinaSkillAxis.Handstream => "Handstream",
                    EzMinaSkillAxis.Stamina => "Stamina",
                    EzMinaSkillAxis.JackSpeed => "JackSpeed",
                    EzMinaSkillAxis.Chordjack => "Chordjack",
                    EzMinaSkillAxis.Technical => "Technical",
                    _ => null,
                };
            }

            return null;
        }
    }

    /// <summary>One hub <c>danSkillsetBuckets</c> entry (filing definition).</summary>
    public sealed class EzDanSkillsetBucketDef
    {
        public EzDanSkillsetBucketDef(
            string id,
            string[] tags,
            string[]? skillsets = null,
            string? clusterFamily = null,
            bool anchor = false)
        {
            Id = id;
            Tags = tags;
            Skillsets = skillsets;
            ClusterFamily = clusterFamily;
            Anchor = anchor;
        }

        public string Id { get; }

        public string[] Tags { get; }

        public string[]? Skillsets { get; }

        /// <summary><c>jack</c> / <c>stream</c> / <c>tech</c>, or null.</summary>
        public string? ClusterFamily { get; }

        public bool Anchor { get; }
    }
}
