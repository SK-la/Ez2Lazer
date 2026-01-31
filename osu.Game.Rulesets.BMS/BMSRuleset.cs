// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Rulesets.BMS.Scoring;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.BMS
{
    public class BMSRuleset : Ruleset
    {
        public override string Description => "BMS - Be-Music Source";

        public override string ShortName => "bms";

        public override string PlayingVerb => "Playing BMS";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableBMSRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
            => new BMSBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
            => new BMSDifficultyCalculator(RulesetInfo, beatmap);

        public override ScoreProcessor CreateScoreProcessor()
            => new BMSScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime)
            => new DrainingHealthProcessor(drainStartTime);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings)
            => new BMSRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings()
            => new BMSSettingsSubsection(this);

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new BMSModEasy(),
                        new BMSModNoFail(),
                    };

                case ModType.DifficultyIncrease:
                    return new Mod[]
                    {
                        new BMSModHardRock(),
                        new BMSModSuddenDeath(),
                    };

                case ModType.Automation:
                    return new Mod[]
                    {
                        new BMSModAutoplay(),
                    };

                case ModType.Fun:
                    return new Mod[]
                    {
                        new BMSModRandom(),
                        new BMSModMirror(),
                    };

                default:
                    return Array.Empty<Mod>();
            }
        }

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => variant switch
        {
            // 5 Keys
            5 => new[]
            {
                new KeyBinding(InputKey.A, BMSAction.Key1),
                new KeyBinding(InputKey.S, BMSAction.Key2),
                new KeyBinding(InputKey.D, BMSAction.Key3),
                new KeyBinding(InputKey.F, BMSAction.Key4),
                new KeyBinding(InputKey.G, BMSAction.Key5),
                new KeyBinding(InputKey.Shift, BMSAction.Scratch1),
            },
            // 7 Keys (default)
            7 => new[]
            {
                new KeyBinding(InputKey.A, BMSAction.Key1),
                new KeyBinding(InputKey.S, BMSAction.Key2),
                new KeyBinding(InputKey.D, BMSAction.Key3),
                new KeyBinding(InputKey.Space, BMSAction.Key4),
                new KeyBinding(InputKey.J, BMSAction.Key5),
                new KeyBinding(InputKey.K, BMSAction.Key6),
                new KeyBinding(InputKey.L, BMSAction.Key7),
                new KeyBinding(InputKey.Shift, BMSAction.Scratch1),
            },
            // 9 Keys (PMS)
            9 => new[]
            {
                new KeyBinding(InputKey.A, BMSAction.Key1),
                new KeyBinding(InputKey.S, BMSAction.Key2),
                new KeyBinding(InputKey.D, BMSAction.Key3),
                new KeyBinding(InputKey.F, BMSAction.Key4),
                new KeyBinding(InputKey.Space, BMSAction.Key5),
                new KeyBinding(InputKey.J, BMSAction.Key6),
                new KeyBinding(InputKey.K, BMSAction.Key7),
                new KeyBinding(InputKey.L, BMSAction.Key8),
                new KeyBinding(InputKey.Semicolon, BMSAction.Key9),
            },
            // 14 Keys (Double Play)
            14 => new[]
            {
                // 1P side
                new KeyBinding(InputKey.A, BMSAction.Key1),
                new KeyBinding(InputKey.S, BMSAction.Key2),
                new KeyBinding(InputKey.D, BMSAction.Key3),
                new KeyBinding(InputKey.F, BMSAction.Key4),
                new KeyBinding(InputKey.G, BMSAction.Key5),
                new KeyBinding(InputKey.H, BMSAction.Key6),
                new KeyBinding(InputKey.J, BMSAction.Key7),
                new KeyBinding(InputKey.Shift, BMSAction.Scratch1),
                // 2P side
                new KeyBinding(InputKey.Keypad7, BMSAction.Key8),
                new KeyBinding(InputKey.Keypad8, BMSAction.Key9),
                new KeyBinding(InputKey.Keypad9, BMSAction.Key10),
                new KeyBinding(InputKey.Keypad4, BMSAction.Key11),
                new KeyBinding(InputKey.Keypad5, BMSAction.Key12),
                new KeyBinding(InputKey.Keypad6, BMSAction.Key13),
                new KeyBinding(InputKey.Keypad1, BMSAction.Key14),
                new KeyBinding(InputKey.Control, BMSAction.Scratch2),
            },
            _ => GetDefaultKeyBindings(7),
        };

        public override IEnumerable<int> AvailableVariants => new[] { 5, 7, 9, 14 };

        public override LocalisableString GetVariantName(int variant) => variant switch
        {
            5 => "5K",
            7 => "7K",
            9 => "9K (PMS)",
            14 => "14K (DP)",
            _ => $"{variant}K"
        };

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.RulesetMania // TODO: Create BMS-specific icon
        };

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}
