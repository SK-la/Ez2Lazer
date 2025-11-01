// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.018;

        private readonly bool isForCurrentRuleset;
        private readonly double originalOverallDifficulty;

        public override int Version => 20241007;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
            originalOverallDifficulty = beatmap.BeatmapInfo.Difficulty.OverallDifficulty;
        }

        public double XxySR { get; set; }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            HitWindows hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            double SR = skills[0].DifficultyValue() * difficulty_multiplier;

            SR = AdditionalMethod(beatmap, mods, skills, clockRate, SR);
            XxySR = SR;

            var attributes = new ManiaDifficultyAttributes
            {
                StarRating = skills.OfType<Strain>().Single().DifficultyValue() * difficulty_multiplier,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject)
            };

            return attributes;
        }

        public double AdditionalMethod(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate, double originalValue)
        {
            double sr = originalValue;

            if (mods.Any(m => m is StarRatingRebirth))
            {
                var beforeCal = DateTime.Now;
                var maniaBeatmap = (ManiaBeatmap)beatmap;
                int keys = (int)maniaBeatmap.Difficulty.CircleSize;
                double od = maniaBeatmap.Difficulty.OverallDifficulty;
                int cs = (int)maniaBeatmap.Difficulty.CircleSize;
                var hit = maniaBeatmap.HitObjects.ToList();
                keys = maniaBeatmap.TotalColumns;

                ManiaModNtoM? ntmMod = null;
                ManiaModNtoMAnother? ntmaMod = null;
                ManiaModDoublePlay? dpMod = null;
                ManiaModAdjust? adjustMod = null;
                StarRatingRebirth? starRatingRebirth = null;

                foreach (var mod in mods)
                {
                    if (mod.GetType() == typeof(ManiaModNtoM)) ntmMod = (ManiaModNtoM)mod;

                    if (mod.GetType() == typeof(ManiaModNtoMAnother)) ntmaMod = (ManiaModNtoMAnother)mod;

                    if (mod.GetType() == typeof(ManiaModDoublePlay)) dpMod = (ManiaModDoublePlay)mod;

                    if (mod.GetType() == typeof(StarRatingRebirth)) starRatingRebirth = (StarRatingRebirth)mod;

                    if (mod.GetType() == typeof(ManiaModAdjust)) adjustMod = (ManiaModAdjust)mod;
                }

                if (mods.Any(m => m is StarRatingRebirth) && cs < keys)
                {
                    if (ntmMod is not null) hit = StarRatingRebirth.NTM(hit, keys, cs)!;
                    /*if (ntma && ntmamod is not null)
                    {
                        hit = StarRatingRebirth.NTMA(beatmap, ntmamod.BlankColumn.Value, keys, ntmamod.Gap.Value, ntmamod.CleanDivide.Value)!;
                    }
                    if (dpmod is not null && cs == 4)
                    {
                        hit = StarRatingRebirth.DP(hit, dpmod.Style.Value);
                    }*/
                    // IDK why NtoM is not working (cannot get the correct HitObjects), but NtoMAnother and DP is working fine.
                }

                try
                {
                    if (starRatingRebirth is not null && keys <= 10 && mods.Any(m => m is StarRatingRebirth) /* && !hasNull*/)
                    {
                        if (starRatingRebirth.Original.Value)
                        {
                            if (adjustMod is not null)
                            {
                                //if (adjustMod.UseBPM.Value)
                                //{
                                //    if (beatmap.BeatmapInfo.BPM == Beatmap.BeatmapInfo.BPM)
                                //    {
                                //        SR = StarRatingRebirth.CalculateStarRating(hit, Beatmap.BeatmapInfo.Difficulty.OverallDifficulty, keys, adjustMod.SpeedChange.Value);
                                //    }
                                //    else
                                //    {
                                //        SR = originalValue;
                                //    }
                                //}
                                //else
                                {
                                    sr = StarRatingRebirth.CalculateStarRating(hit, Beatmap.BeatmapInfo.Difficulty.OverallDifficulty, keys, adjustMod.SpeedChange.Value);
                                }
                            }
                            else
                                sr = StarRatingRebirth.CalculateStarRating(hit, od, keys, clockRate);
                        }
                        else if (starRatingRebirth.Custom.Value)
                        {
                            if (adjustMod is not null)
                            {
                                //if (adjustMod.UseBPM.Value)
                                //{
                                //    if (beatmap.BeatmapInfo.BPM == Beatmap.BeatmapInfo.BPM)
                                //    {
                                //        SR = StarRatingRebirth.CalculateStarRating(hit, starRatingRebirth.OD.Value, keys, adjustMod.SpeedChange.Value);
                                //    }
                                //    else
                                //    {
                                //        SR = originalValue;
                                //    }
                                //}
                                //else
                                {
                                    sr = StarRatingRebirth.CalculateStarRating(hit, starRatingRebirth.OD.Value, keys, adjustMod.SpeedChange.Value);
                                }
                            }
                            else
                                sr = StarRatingRebirth.CalculateStarRating(hit, starRatingRebirth.OD.Value, keys, clockRate);
                        }
                        else
                            sr = StarRatingRebirth.CalculateStarRating(hit, od, keys, clockRate);
                    }
                    else
                        sr = skills.OfType<Strain>().Single().DifficultyValue() * difficulty_multiplier;
                }
                catch
                {
                    try
                    {
                        sr = skills.OfType<Strain>().Single().DifficultyValue() * difficulty_multiplier;
                    }
                    catch
                    {
                        sr = 0;
                    }
                }

                var afterCal = DateTime.Now;
                // Logger.Log(beatmap.Metadata.Title + " \n" + beatmap.BeatmapInfo.DifficultyName + "\n Elapsed Time: " + (afterCal - beforeCal).ToString(), level: LogLevel.Important);
            }

            return sr;
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            LegacySortHelper<HitObject>.Sort(sortedObjects, Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[]
            {
                new Strain(mods, ((ManiaBeatmap)Beatmap).TotalColumns)
            };

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),
                };

                if (isForCurrentRuleset)
                    return mods;

                // if we are a convert, we can be played in any key mod.
                return mods.Concat(new Mod[]
                {
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                }).ToArray();
            }
        }
    }
}
