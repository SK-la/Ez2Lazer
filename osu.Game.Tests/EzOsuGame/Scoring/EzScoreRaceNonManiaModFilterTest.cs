// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRaceNonManiaModFilterTest
    {
        [Test]
        public void NonManiaSelectGhostCandidatesIgnoresSameAsCurrentFilter()
        {
            // 产品契约：非 Mania 不经 Mod 过滤。此处用查询层模拟 Service 强制 Any 后的结果：
            // SameAsCurrent 若被误用会丢掉 HR 幽灵；Any 则保留。
            var ruleset = new OsuRuleset();
            var hr = ruleset.CreateMod<OsuModHardRock>()!;
            var nm = new ScoreInfo { TotalScore = 1_000_000, Mods = Array.Empty<Mod>() };
            var hrScore = new ScoreInfo { TotalScore = 900_000, Mods = new Mod[] { hr } };

            var withAny = EzLocalScoreQueries.SelectGhostCandidates(
                new[] { nm, hrScore },
                Array.Empty<Mod>(),
                EzScoreModFilter.Any,
                maxEntries: 10);

            Assert.That(withAny.Count, Is.EqualTo(2));

            var ifSameAsCurrent = EzLocalScoreQueries.SelectGhostCandidates(
                new[] { nm, hrScore },
                Array.Empty<Mod>(),
                EzScoreModFilter.SameAsCurrent,
                maxEntries: 10);

            Assert.That(ifSameAsCurrent.Count, Is.EqualTo(1));
            Assert.That(ifSameAsCurrent.Single().TotalScore, Is.EqualTo(1_000_000));
        }
    }
}
