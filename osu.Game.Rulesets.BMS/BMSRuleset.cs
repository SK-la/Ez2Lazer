// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Mods;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Scoring;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Filter;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using System.IO;

namespace osu.Game.Rulesets.BMS
{
    public class BMSRuleset : Ruleset
    {
        static BMSRuleset()
        {
            BMSBeatmapDecoder.Register();
        }

        public override string Description => "BMS - Be-Music Source";

        public override string ShortName => "bms";

        public override string PlayingVerb => "Playing BMS";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new ManiaRuleset().CreateDrawableRulesetWith(ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(beatmap), mods);

        public override ScoreProcessor CreateScoreProcessor() => new BMSScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => new BMSNoFailHealthProcessor(drainStartTime);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new BMSBeatmapConverter(beatmap, this);
        public override PerformanceCalculator CreatePerformanceCalculator() => new ManiaPerformanceCalculator();

        public override HitObjectComposer CreateHitObjectComposer() => new ManiaHitObjectComposer(this);

        public override IBeatmapVerifier CreateBeatmapVerifier() => new ManiaBeatmapVerifier();

        public override IEzAnalysisProvider CreateEzAnalysisProvider() => new EzManiaAnalysisProvider();

        public override ISkin? CreateSkinTransformer(ISkin skin, IBeatmap beatmap)
        {
            var maniaRuleset = new ManiaRuleset();

            try
            {
                return maniaRuleset.CreateSkinTransformer(skin, beatmap) ?? base.CreateSkinTransformer(skin, beatmap);
            }
            catch (InvalidCastException) when (beatmap is not ManiaBeatmap)
            {
                // Some mania transformers assume ManiaBeatmap; provide an adapted beatmap for BMS.
                var adaptedBeatmap = createManiaSkinBeatmap(beatmap);

                try
                {
                    return maniaRuleset.CreateSkinTransformer(skin, adaptedBeatmap) ?? base.CreateSkinTransformer(skin, beatmap);
                }
                catch
                {
                    // Transformer-specific assumptions may still fail (e.g. custom skin configs).
                    return base.CreateSkinTransformer(skin, beatmap);
                }
            }
            catch
            {
                // Never let skin transformer failures crash BMS gameplay startup.
                return base.CreateSkinTransformer(skin, beatmap);
            }
        }

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
            => new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, new ManiaDifficultyWorkingBeatmapAdapter(beatmap));

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => new ManiaReplayFrame();

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new BMSRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() => new BMSSettingsSubsection(this);

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new ManiaModEasy(),
                        new ManiaModNoFail(),
                        new ManiaModHalfTime(),
                    };

                case ModType.DifficultyIncrease:
                    return new Mod[]
                    {
                        new ManiaModHardRock(),
                        new ManiaModSuddenDeath(),
                        new ManiaModPerfect(),
                        new ManiaModDoubleTime(),
                        new ManiaModNightcore(),
                        new ManiaModHidden(),
                        new ManiaModFadeIn(),
                        new ManiaModFlashlight(),
                    };

                case ModType.Automation:
                    return new Mod[]
                    {
                        new ManiaModAutoplay(),
                    };

                case ModType.Conversion:
                    return new Mod[]
                    {
                        new BMSModRandom(),
                        new BMSModMirror(),
                        new BMSModScratchLaneRight(),
                    };

                case ModType.Fun:
                    return Array.Empty<Mod>();

                default:
                    return Array.Empty<Mod>();
            }
        }

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0)
        {
            int totalColumns = Math.Clamp(variant <= 0 ? 8 : variant, 1, 16);

            var maniaRuleset = new ManiaRuleset();
            var maniaBindings = maniaRuleset.GetDefaultKeyBindings((int)PlayfieldType.Single + totalColumns);

            foreach (var binding in maniaBindings)
            {
                if (binding.Action is not ManiaAction maniaAction)
                    continue;
                if (binding.KeyCombination.Equals(InputKey.None))
                    continue;

                int index = (int)maniaAction;
                BMSAction mapped = index switch
                {
                    <= 13 => (BMSAction)((int)BMSAction.Key1 + index),
                    14 => BMSAction.Scratch1,
                    15 => BMSAction.Scratch2,
                    _ => BMSAction.Scratch2
                };

                yield return new KeyBinding(binding.KeyCombination, mapped);
            }
        }

        public override LocalisableString VariantDescription => "Keys";

        public override IEnumerable<int> AvailableVariants => Enumerable.Range(1, 16);

        public override LocalisableString GetVariantName(int variant) => variant switch
        {
            > 0 => $"{variant}K",
            _ => "8K"
        };

        public override IEnumerable<HitResult> GetValidHitResults()
        {
            return HitModeHelper.GetHitModeValidHitResults();
        }

        public override LocalisableString GetDisplayNameForHitResult(HitResult result)
        {
            // 获取对应 HitMode 的显示名称
            return result.GetHitModeDisplayName();
        }

        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap) => new[]
        {
            new StatisticItem("Performance Breakdown", () => new PerformanceBreakdownChart(score, playableBeatmap)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y
            }),
            new StatisticItem("Space Graph", () => new EzScoreGraphMania(score, playableBeatmap)
            {
                RelativeSizeAxes = Axes.X,
                Height = 200
            }, true),
            new StatisticItem("Timing Distribution", () => new HitEventTimingDistributionGraph(score.HitEvents)
            {
                RelativeSizeAxes = Axes.X,
                Height = 120
            }, true),
            new StatisticItem("Every Column Timing Graphs", () => new EzScoreEveryColumnTimingGraphs(score)
            {
                RelativeSizeAxes = Axes.X,
                Height = 250,
            }, true),
            new StatisticItem("HitResult Count", () => new EzManiaScoreHitResultCountGraph(score)
            {
                RelativeSizeAxes = Axes.X
            }, true),
            new StatisticItem("Statistics", () => new SimpleStatisticTable(2, new SimpleStatisticItem[]
            {
                new AverageHitError(score.HitEvents),
                new UnstableRate(score.HitEvents)
            }), true)
        };

        /// <seealso cref="ManiaHitWindows"/>
        public override BeatmapDifficulty GetAdjustedDisplayDifficulty(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods)
        {
            BeatmapDifficulty adjustedDifficulty = base.GetAdjustedDisplayDifficulty(beatmapInfo, mods);

            // notably, in mania, hit windows are designed to be independent of track playback rate (see `ManiaHitWindows.SpeedMultiplier`).
            // *however*, to not make matters *too* simple, mania Hard Rock and Easy differ from all other rulesets
            // in that they apply multipliers *to hit window durations directly* rather than to the Overall Difficulty attribute itself.
            // because the duration of hit window durations as a function of OD is not a linear function,
            // this means that multiplying the OD is *not* the same thing as multiplying the hit window duration.
            // in fact, the second operation is *much* harsher and will produce values much farther outside of normal operating range
            // (even negative in the case of Easy).
            // stable handles this wrong on song select and just assumes that it can handle mania EZ / HR the same way as all other rulesets.

            double perfectHitWindow = IBeatmapDifficultyInfo.DifficultyRange(adjustedDifficulty.OverallDifficulty, ManiaHitWindows.PERFECT_WINDOW_RANGE);

            if (mods.Any(m => m is ManiaModHardRock))
                perfectHitWindow /= ManiaModHardRock.HIT_WINDOW_DIFFICULTY_MULTIPLIER;
            else if (mods.Any(m => m is ManiaModEasy))
                perfectHitWindow /= ManiaModEasy.HIT_WINDOW_DIFFICULTY_MULTIPLIER;

            adjustedDifficulty.OverallDifficulty = (float)IBeatmapDifficultyInfo.InverseDifficultyRange(perfectHitWindow, ManiaHitWindows.PERFECT_WINDOW_RANGE);
            adjustedDifficulty.CircleSize = ManiaBeatmapConverter.GetColumnCount(LegacyBeatmapConversionDifficultyInfo.FromBeatmapInfo(beatmapInfo), mods);

            return adjustedDifficulty;
        }

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods)
        {
            // 清理残留
            ManiaHitWindows.ClearModOverride();

            // a special touch-up of key count is required to the original difficulty, since key conversion mods are not `IApplicableToDifficulty`
            var originalDifficulty = new BeatmapDifficulty(beatmapInfo.Difficulty)
            {
                CircleSize = ManiaBeatmapConverter.GetColumnCount(LegacyBeatmapConversionDifficultyInfo.FromBeatmapInfo(beatmapInfo), [])
            };
            var adjustedDifficulty = GetAdjustedDisplayDifficulty(beatmapInfo, mods);

            // 单独接口追踪Mod中的变化
            var range = mods.OfType<IManiaHitRangeProvider>().FirstOrDefault()?.GetDisplayHitRange(beatmapInfo);

            if (range != null)
                ManiaHitWindows.SetModOverride(range.Value);

            var colours = new OsuColour();

            yield return new RulesetBeatmapAttribute(SongSelectStrings.KeyCount, @"KC", originalDifficulty.CircleSize, adjustedDifficulty.CircleSize, 18)
            {
                Description = "Affects the number of key columns on the playfield."
            };

            var hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(adjustedDifficulty.OverallDifficulty);
            hitWindows.IsConvert = !beatmapInfo.Ruleset.Equals(RulesetInfo);
            hitWindows.ClassicModActive = mods.Any(m => m is ManiaModClassic);
            hitWindows.BPM = beatmapInfo.BPM;

            // 获取当前HitMode
            var currentHitMode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            bool isBMSMode = HitModeHelper.IsBMSHitMode(currentHitMode);

            yield return new RulesetBeatmapAttribute(SongSelectStrings.Accuracy, @"OD", originalDifficulty.OverallDifficulty, adjustedDifficulty.OverallDifficulty, 10)
            {
                Description = "Affects timing requirements for notes.",
                AdditionalMetrics = hitWindows.GetAllAvailableWindows()
                                              .Reverse()
                                              .Select(window =>
                                              {
                                                  LocalisableString windowText;

                                                  if (isBMSMode)
                                                  {
                                                      // BMS模式：根据数值是否相等选择显示格式
                                                      double earlyWindow = hitWindows.WindowFor(window.result, true);
                                                      double lateWindow = hitWindows.WindowFor(window.result, false);

                                                      if (Math.Abs(earlyWindow - lateWindow) > 0.01)
                                                          windowText = $@"-{earlyWindow:0.##}/+{lateWindow:0.##} ms";
                                                      else
                                                          windowText = $@"±{earlyWindow:0.##} ms";
                                                  }
                                                  else
                                                  {
                                                      // 非BMS模式：显示对称格式
                                                      double d = hitWindows.WindowFor(window.result);
                                                      windowText = $@"±{d:0.##} ms";
                                                  }

                                                  return new RulesetBeatmapAttribute.AdditionalMetric(
                                                      $"{window.result.GetHitModeDisplayName().ToString().ToUpperInvariant()} hit window",
                                                      windowText,
                                                      colours.ForHitResult(window.result)
                                                  );
                                              }).ToArray()
            };

            yield return new RulesetBeatmapAttribute(SongSelectStrings.HPDrain, @"HP", originalDifficulty.DrainRate, adjustedDifficulty.DrainRate, 10)
            {
                Description = "Affects the harshness of health drain and the health penalties for missing."
            };
        }

        public override IRulesetFilterCriteria CreateRulesetFilterCriteria()
        {
            return new ManiaFilterCriteria();
        }

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.RulesetMania // TODO: Create BMS-specific icon
        };

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        private static ManiaBeatmap createManiaSkinBeatmap(IBeatmap beatmap)
        {
            int totalColumns = Math.Max(1, getManiaSkinTotalColumns(beatmap));
            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(totalColumns))
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                ControlPointInfo = beatmap.ControlPointInfo,
            };

            foreach (BMSHitObject obj in beatmap.HitObjects.OfType<BMSHitObject>())
            {
                ManiaHitObject converted = obj switch
                {
                    BMSHoldNote hold => new HoldNote
                    {
                        StartTime = hold.StartTime,
                        Duration = hold.Duration,
                        Column = hold.Column
                    },
                    _ => new Note
                    {
                        StartTime = obj.StartTime,
                        Column = obj.Column
                    }
                };

                maniaBeatmap.HitObjects.Add(converted);
            }

            return maniaBeatmap;
        }

        private static int getManiaSkinTotalColumns(IBeatmap beatmap)
        {
            int fromLayout = BMSStageLayout.FromBeatmap(beatmap).TotalColumns;
            int fromBeatmap = beatmap is BMSBeatmap bmsBeatmap ? bmsBeatmap.TotalColumns : 0;
            int fromDifficulty = (int)Math.Round(beatmap.Difficulty.CircleSize);
            int fromHitObjects = beatmap.HitObjects.OfType<BMSHitObject>().Select(h => h.Column + 1).DefaultIfEmpty(0).Max();
            return Math.Max(Math.Max(fromLayout, fromBeatmap), Math.Max(fromDifficulty, fromHitObjects));
        }
    }

    internal class BMSNativeRuleset : BMSRuleset
    {
        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableBMSRuleset(this, beatmap, mods);
    }

    internal class ManiaDifficultyWorkingBeatmapAdapter : WorkingBeatmap
    {
        private readonly IWorkingBeatmap source;
        private readonly ManiaBeatmap maniaBeatmap;

        public ManiaDifficultyWorkingBeatmapAdapter(IWorkingBeatmap source)
            : base((BeatmapInfo)source.BeatmapInfo, null)
        {
            this.source = source;
            maniaBeatmap = ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(source.Beatmap);
        }

        protected override IBeatmap GetBeatmap() => maniaBeatmap;

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token) => maniaBeatmap;

        public override Texture? GetBackground() => source.GetBackground();

        protected override Track GetBeatmapTrack() => source.Track;

        protected override ISkin GetSkin() => source.Skin;

        public override Stream? GetStream(string storagePath) => source.GetStream(storagePath);
    }
}
