// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Beatmaps
{
    [HeadlessTest]
    public partial class TestSceneBeatmapDifficultyCache : OsuTestScene
    {
        public const double BASE_STARS = 5.55;

        private static readonly Guid guid = Guid.NewGuid();

        private BeatmapSetInfo importedSet;

        private TestBeatmapDifficultyCache difficultyCache;

        private IBindable<StarDifficulty> starDifficultyBindable;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; }

        [Resolved]
        private BeatmapDifficultyCache actualDifficultyCache { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuGameBase osu)
        {
            importedSet = BeatmapImportHelper.LoadQuickOszIntoOsu(osu).GetResultSafely();
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup difficulty cache", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();

                Child = difficultyCache = new TestBeatmapDifficultyCache();

                starDifficultyBindable = difficultyCache.GetBindableDifficulty(importedSet.Beatmaps.First());
            });

            AddUntilStep($"star difficulty -> {BASE_STARS}", () => starDifficultyBindable.Value.Stars == BASE_STARS);
        }

        [Test]
        [FlakyTest] // one fix attempted in https://github.com/ppy/osu/pull/37178, didn't work
        public void TestInvalidationFlow()
        {
            BeatmapInfo postEditBeatmapInfo = null;
            BeatmapInfo preEditBeatmapInfo = null;

            IBindable<StarDifficulty> bindableDifficulty = null;

            AddStep("get bindable stars", () =>
            {
                preEditBeatmapInfo = importedSet.Beatmaps.First();
                bindableDifficulty = actualDifficultyCache.GetBindableDifficulty(preEditBeatmapInfo);
            });

            AddUntilStep("wait for stars retrieved", () => bindableDifficulty.Value.Stars, () => Is.GreaterThan(0));

            AddStep("remove all hitobjects", () =>
            {
                var working = beatmapManager.GetWorkingBeatmap(preEditBeatmapInfo);

                ((IList<HitObject>)working.Beatmap.HitObjects).Clear();

                beatmapManager.Save(working.BeatmapInfo, working.Beatmap);
                postEditBeatmapInfo = working.BeatmapInfo;
            });

            AddAssert("stars is now zero", () => actualDifficultyCache.GetDifficultyAsync(postEditBeatmapInfo).GetResultSafely()!.Value.Stars, () => Is.Zero);
            AddUntilStep("bindable stars is now zero", () => bindableDifficulty.Value.Stars, () => Is.Zero);
        }

        [Test]
        public void TestStarDifficultyChangesOnModSettings()
        {
            OsuModDoubleTime dt = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modRateAdjust = (ModRateAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModRateAdjust);
                return new StarDifficulty(BASE_STARS + modRateAdjust?.SpeedChange.Value ?? 0, 0);
            });

            AddStep("change selected mod to DT", () => SelectedMods.Value = new[] { dt = new OsuModDoubleTime { SpeedChange = { Value = 1.5 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.5}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.5);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 1.25);
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("change selected mod to NC", () => SelectedMods.Value = new[] { new OsuModNightcore { SpeedChange = { Value = 1.75 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.75}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.75);
        }

        [Test]
        public void TestStarDifficultyChangesOnModSettingsCorrectlyTrackAcrossReferenceChanges()
        {
            OsuModDoubleTime dt = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modRateAdjust = (ModRateAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModRateAdjust);
                return new StarDifficulty(BASE_STARS + modRateAdjust?.SpeedChange.Value ?? 0, 0);
            });

            AddStep("change selected mod to DT", () => SelectedMods.Value = new[] { dt = new OsuModDoubleTime { SpeedChange = { Value = 1.5 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.5}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.5);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 1.25);
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("reconstruct DT mod with same settings", () => SelectedMods.Value = new[] { dt = (OsuModDoubleTime)dt.DeepClone() });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 2);
            AddUntilStep($"star difficulty -> {BASE_STARS + 2}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 2);
        }

        [Test]
        public void TestStarDifficultyAdjustHashCodeConflict()
        {
            OsuModDifficultyAdjust difficultyAdjust = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modDifficultyAdjust = (ModDifficultyAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModDifficultyAdjust);
                return new StarDifficulty(BASE_STARS * (modDifficultyAdjust?.OverallDifficulty.Value ?? 1), 0);
            });

            AddStep("change selected mod to DA", () => SelectedMods.Value = new[] { difficultyAdjust = new OsuModDifficultyAdjust() });
            AddUntilStep($"star difficulty -> {BASE_STARS}", () => starDifficultyBindable.Value.Stars == BASE_STARS);

            AddStep("change DA difficulty to 0.5", () => difficultyAdjust.OverallDifficulty.Value = 0.5f);
            AddUntilStep($"star difficulty -> {BASE_STARS * 0.5f}", () => starDifficultyBindable.Value.Stars == BASE_STARS / 2);

            // hash code of 0 (the value) conflicts with the hash code of null (the initial/default value).
            // it's important that the mod reference and its underlying bindable references stay the same to demonstrate this failure.
            AddStep("change DA difficulty to 0", () => difficultyAdjust.OverallDifficulty.Value = 0);
            AddUntilStep("star difficulty -> 0", () => starDifficultyBindable.Value.Stars == 0);
        }

        [Test]
        public void TestKeyEqualsWithDifferentModInstances()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });

            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyNotEqualWithDifferentModOrder()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHidden(), new OsuModHardRock() });

            // 转换只对 ApplyOrder 做稳定排序，同 order 的 mod 按列表序生效，所以两种顺序是两个不同输入。
            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyPreservesCallerModOrder()
        {
            var key = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHidden(), new OsuModHardRock() });

            Assert.That(key.OrderedMods.Select(m => m.Acronym), Is.EqualTo(new[] { "HD", "HR" }));
        }

        [Test]
        public void TestKeyNotEqualWithDifferentBeatmapContentHash()
        {
            // 同一 BeatmapInfo.ID 但内容已换（重新导入 / 外部同步）：不能命中旧难度。
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid, Hash = "hash-a" }, new RulesetInfo { OnlineID = 0 }, Array.Empty<Mod>());
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid, Hash = "hash-b" }, new RulesetInfo { OnlineID = 0 }, Array.Empty<Mod>());

            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyDoesntEqualWithDifferentModSettings()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.1 } } });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.9 } } });

            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
        }

        /// <summary>
        /// 「设置未填（null）」与「设置填 0」在指纹里会撞哈希（boxed <c>0f</c> 的 GetHashCode 就是 0），
        /// 所以键的相等必须是真正的设置比较，不能只看指纹——否则会把一种设置的难度当成另一种返回。
        /// </summary>
        [Test]
        public void TestKeyNotEqualWhenSettingHashesCollide()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDifficultyAdjust() });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDifficultyAdjust { OverallDifficulty = { Value = 0 } } });

            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()),
                "前置条件：本用例正是要覆盖哈希相同但设置不同的情形");
        }

        [Test]
        public void TestKeyEqualWithMatchingModSettings()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.25 } } });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.25 } } });

            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
        }

        [TestCase(1.3, DifficultyRating.Easy)]
        [TestCase(1.993, DifficultyRating.Easy)]
        [TestCase(1.998, DifficultyRating.Normal)]
        [TestCase(2.4, DifficultyRating.Normal)]
        [TestCase(2.693, DifficultyRating.Normal)]
        [TestCase(2.698, DifficultyRating.Hard)]
        [TestCase(3.5, DifficultyRating.Hard)]
        [TestCase(3.993, DifficultyRating.Hard)]
        [TestCase(3.997, DifficultyRating.Insane)]
        [TestCase(5.0, DifficultyRating.Insane)]
        [TestCase(5.292, DifficultyRating.Insane)]
        [TestCase(5.297, DifficultyRating.Expert)]
        [TestCase(6.2, DifficultyRating.Expert)]
        [TestCase(6.493, DifficultyRating.Expert)]
        [TestCase(6.498, DifficultyRating.ExpertPlus)]
        [TestCase(8.3, DifficultyRating.ExpertPlus)]
        public void TestDifficultyRatingMapping(double starRating, DifficultyRating expectedBracket)
        {
            var actualBracket = StarDifficulty.GetDifficultyRating(starRating);

            ClassicAssert.AreEqual(expectedBracket, actualBracket);
        }

        private partial class TestBeatmapDifficultyCache : BeatmapDifficultyCache
        {
            public Func<DifficultyCacheLookup, StarDifficulty> ComputeDifficulty { get; set; }

            protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken token = default)
            {
                return Task.FromResult<StarDifficulty?>(ComputeDifficulty?.Invoke(lookup) ?? new StarDifficulty(BASE_STARS, 0));
            }
        }
    }
}
