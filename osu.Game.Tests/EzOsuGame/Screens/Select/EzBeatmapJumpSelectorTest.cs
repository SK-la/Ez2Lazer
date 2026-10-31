// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Select;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Screens.Select
{
    /// <summary>
    /// <see cref="EzBeatmapJumpSelector"/> 的两条契约：跳转必须留在正在播放的那首歌里，
    /// 且优先保持当前活动规则集。
    /// </summary>
    [TestFixture]
    public class EzBeatmapJumpSelectorTest
    {
        private RulesetInfo maniaRuleset = null!;
        private RulesetInfo osuRuleset = null!;

        [SetUp]
        public void SetUp()
        {
            maniaRuleset = new ManiaRuleset().RulesetInfo;
            osuRuleset = new OsuRuleset().RulesetInfo;
        }

        [Test]
        public void TestPlayingDifficultyOnActiveRulesetIsKept()
        {
            var set = new BeatmapSetInfo();
            var playing = addDifficulty(set, maniaRuleset, "playing");
            addDifficulty(set, maniaRuleset, "other");

            var result = EzBeatmapJumpSelector.Select(workingBeatmap(playing), maniaRuleset);

            Assert.That(result, Is.EqualTo(playing), "正在播放的难度属于活动规则集时不得被换掉");
        }

        [Test]
        public void TestFallsBackToPlayingDifficultyWhenActiveRulesetHasNoDifficulty()
        {
            var set = new BeatmapSetInfo();
            var playing = addDifficulty(set, osuRuleset, "playing");

            var result = EzBeatmapJumpSelector.Select(workingBeatmap(playing), maniaRuleset);

            Assert.That(result, Is.EqualTo(playing), "该歌在活动规则集下没有难度时，应跟随正在播放的难度并切换规则集");
        }

        [Test]
        public void TestPrefersActiveRulesetDifficultyOfSameSong()
        {
            var set = new BeatmapSetInfo();
            var playing = addDifficulty(set, osuRuleset, "playing");
            var maniaA = addDifficulty(set, maniaRuleset, "a");
            var maniaB = addDifficulty(set, maniaRuleset, "b");

            var result = EzBeatmapJumpSelector.Select(workingBeatmap(playing), maniaRuleset);

            Assert.That(result!.Ruleset, Is.EqualTo(maniaRuleset), "应优先保持活动规则集，而不是切到正在播放的规则集");
            Assert.That(result, Is.AnyOf(maniaA, maniaB));
        }

        [Test]
        public void TestResultAlwaysBelongsToPlayingSet()
        {
            var set = new BeatmapSetInfo();
            var playing = addDifficulty(set, osuRuleset, "playing");
            addDifficulty(set, maniaRuleset, "a");

            var result = EzBeatmapJumpSelector.Select(workingBeatmap(playing), maniaRuleset);

            Assert.That(result!.BeatmapSet, Is.EqualTo(set), "跳转结果必须留在正在播放的那首歌里");
        }

        [Test]
        public void TestEmptySetResolvesNothing()
        {
            var set = new BeatmapSetInfo();
            var playing = addDifficulty(set, maniaRuleset, "playing");
            set.Beatmaps.Clear();

            Assert.That(EzBeatmapJumpSelector.Select(workingBeatmap(playing), maniaRuleset), Is.Null);
        }

        private static BeatmapInfo addDifficulty(BeatmapSetInfo set, RulesetInfo ruleset, string difficultyName)
        {
            var beatmapInfo = new BeatmapInfo(ruleset)
            {
                DifficultyName = difficultyName,
                BeatmapSet = set,
            };

            set.Beatmaps.Add(beatmapInfo);
            return beatmapInfo;
        }

        private static WorkingBeatmap workingBeatmap(BeatmapInfo beatmapInfo) => new TestWorkingBeatmap(new Beatmap { BeatmapInfo = beatmapInfo });
    }
}
