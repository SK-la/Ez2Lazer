// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestEzRealmAccessSchemaVersion : RealmTest
    {
        [Test]
        public void TestOpensWithEzFileSchemaVersion()
        {
            RunTestWithRealm((realm, _) =>
            {
                realm.Run(r =>
                {
                    Assert.That(r.Config.SchemaVersion, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
                });
            });
        }

        /// <summary>
        /// ez v11 重排：旧枚举值（0..9）必须映射到重排后的新值，负值（未设置）保持。
        /// </summary>
        [TestCase(-1, -1)]
        [TestCase(0, (int)EzEnumJudgeMode.Lazer)]
        [TestCase(1, (int)EzEnumJudgeMode.EZ2AC)]
        [TestCase(2, (int)EzEnumJudgeMode.O2Jam)]
        [TestCase(3, (int)EzEnumJudgeMode.IIDX_HD)]
        [TestCase(4, (int)EzEnumJudgeMode.LR2_HD)]
        [TestCase(5, (int)EzEnumJudgeMode.Raja_NM)]
        [TestCase(6, (int)EzEnumJudgeMode.Malody_E)]
        [TestCase(7, (int)EzEnumJudgeMode.Malody_B)]
        [TestCase(8, RealmAccess.LEGACY_CLASSIC_JUDGE_MODE)]
        [TestCase(9, (int)EzEnumJudgeMode.StableClassic)]
        public void TestRemapEzJudgeMode(int oldValue, int expected) => Assert.That(RealmAccess.RemapEzJudgeMode(oldValue), Is.EqualTo(expected));

        /// <summary>
        /// ez v11 重排：血量档旧枚举值（0..7）→ 新值，负值保持。
        /// </summary>
        [TestCase(-1, -1)]
        [TestCase(0, (int)EzEnumHealthMode.Lazer)]
        [TestCase(1, (int)EzEnumHealthMode.O2JamEasy)]
        [TestCase(2, (int)EzEnumHealthMode.O2JamNormal)]
        [TestCase(3, (int)EzEnumHealthMode.O2JamHard)]
        [TestCase(4, (int)EzEnumHealthMode.Ez2Ac)]
        [TestCase(5, (int)EzEnumHealthMode.IIDX_HD)]
        [TestCase(6, (int)EzEnumHealthMode.LR2_HD)]
        [TestCase(7, (int)EzEnumHealthMode.Raja_HD)]
        public void TestRemapEzHealthMode(int oldValue, int expected) => Assert.That(RealmAccess.RemapEzHealthMode(oldValue), Is.EqualTo(expected));
    }
}
