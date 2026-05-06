// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
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
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

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

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) => new DrawableBMSRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new BMSBeatmapConverter(beatmap, this);

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

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new BMSDifficultyCalculator(RulesetInfo, beatmap);

        public override ScoreProcessor CreateScoreProcessor() => new BMSScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => new DrainingHealthProcessor(drainStartTime);

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

        public override IEnumerable<int> AvailableVariants => Enumerable.Range(1, 16);

        public override LocalisableString GetVariantName(int variant) => variant switch
        {
            > 0 => $"{variant}K",
            _ => "8K"
        };

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
}
