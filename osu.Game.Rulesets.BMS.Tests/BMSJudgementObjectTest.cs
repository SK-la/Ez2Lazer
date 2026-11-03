// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Judgements;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSJudgementObjectTest
    {
        [Test]
        public void TestBMSNoteUsesRegularJudgement()
        {
            var note = new BMSNote();

            Assert.That(note.CreateJudgement(), Is.TypeOf<Judgement>());
        }

        [Test]
        public void TestBMSHoldNoteUsesIgnoreJudgementForParent()
        {
            var hold = new BMSHoldNote();

            Assert.That(hold.CreateJudgement(), Is.TypeOf<IgnoreJudgement>());
        }

        [Test]
        public void TestBMSHoldNoteBodyUsesIgnoreJudgement()
        {
            var body = new BMSHoldNoteBody();

            Assert.That(body.CreateJudgement(), Is.TypeOf<IgnoreJudgement>());
        }

        [Test]
        public void TestBMSHoldNoteTailUsesRegularJudgement()
        {
            var tail = new BMSHoldNoteTail();

            Assert.That(tail.CreateJudgement(), Is.TypeOf<Judgement>());
        }
    }
}
