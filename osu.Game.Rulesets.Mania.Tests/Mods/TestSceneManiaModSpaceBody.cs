// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    public partial class TestSceneManiaModSpaceBody : ModTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [Test]
        public void TestDefaultSettings()
        {
            var mod = new ManiaModSpaceBody();

            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () => new Beatmap
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                    HitObjects = new List<HitObject>
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new Note { StartTime = 2000, Column = 0 },
                        new Note { StartTime = 3000, Column = 0 },
                        new Note { StartTime = 1000, Column = 1 },
                        new Note { StartTime = 2500, Column = 1 },
                        new Note { StartTime = 1000, Column = 2 },
                        new Note { StartTime = 3500, Column = 2 }
                    }
                },
                PassCondition = () => Player.DrawableRuleset?.Objects.Any() == true
            });
        }

        [Test]
        public void TestWithSmallSpaceBeat()
        {
            var mod = new ManiaModSpaceBody
            {
                SpaceBeat = { Value = 2.0 }
            };

            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () => new Beatmap
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                    HitObjects = new List<HitObject>
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new Note { StartTime = 2000, Column = 0 },
                        new Note { StartTime = 3000, Column = 0 }
                    }
                },
                PassCondition = () => Player.DrawableRuleset?.Objects.Any() == true
            });
        }

        [Test]
        public void TestWithLargeSpaceBeat()
        {
            var mod = new ManiaModSpaceBody
            {
                SpaceBeat = { Value = 8.0 }
            };

            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () => new Beatmap
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                    HitObjects = new List<HitObject>
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new Note { StartTime = 2000, Column = 0 },
                        new Note { StartTime = 3000, Column = 0 }
                    }
                },
                PassCondition = () => Player.DrawableRuleset?.Objects.Any() == true
            });
        }

        [Test]
        public void TestWithShieldEnabled()
        {
            var mod = new ManiaModSpaceBody
            {
                Shield = { Value = true }
            };

            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () => new Beatmap
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                    HitObjects = new List<HitObject>
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new Note { StartTime = 2000, Column = 0 },
                        new Note { StartTime = 3000, Column = 0 }
                    }
                },
                PassCondition = () => Player.DrawableRuleset?.Objects.Any() == true
            });
        }

        [Test]
        public void TestWithHoldNotes()
        {
            var mod = new ManiaModSpaceBody();

            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () => new Beatmap
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                    HitObjects = new List<HitObject>
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new HoldNote { StartTime = 2000, Duration = 500, Column = 0 },
                        new Note { StartTime = 3000, Column = 0 }
                    }
                },
                PassCondition = () => Player.DrawableRuleset?.Objects.Any() == true
            });
        }
    }
}