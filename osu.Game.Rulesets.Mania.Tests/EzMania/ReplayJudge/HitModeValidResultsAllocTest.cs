// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class JudgeModeValidResultsAllocTest
    {
        [Test]
        public void GetJudgeModeValidHitResultsReturnsCachedInstance()
        {
            var a = JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.Lazer);
            var b = JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.Lazer);
            Assert.That(ReferenceEquals(a, b), Is.True);
        }

        [Test]
        public void GetJudgeModeValidHitResultsDoesNotAllocate()
        {
            // warm
            JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.Lazer);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 50_000; i++)
            {
                JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.Lazer);
                JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.IIDX_HD);
                JudgeModeHelper.GetJudgeModeValidHitResults(EzEnumJudgeMode.O2Jam);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.EqualTo(0), $"Expected zero alloc, got {after - before}B");
        }
    }
}
