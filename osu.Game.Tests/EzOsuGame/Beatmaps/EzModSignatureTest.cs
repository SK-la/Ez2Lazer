// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.EzOsuGame.Beatmaps
{
    /// <summary>
    /// <see cref="EzModSignature"/> 是难度 / 分析 / 仿真三处缓存键共用的 mods 指纹，这里钉住它的三条契约：
    /// 顺序敏感、含解析后的 seed、随设置变化。任何一条被改回去都会让不同输入撞同一个键。
    /// </summary>
    [TestFixture]
    public class EzModSignatureTest
    {
        [Test]
        public void TestSignatureIsOrderSensitive()
        {
            var first = new Mod[] { new ManiaModNtoM(), new ManiaModCleanColumn() };
            var second = new Mod[] { new ManiaModCleanColumn(), new ManiaModNtoM() };

            Assert.That(EzModSignature.Compute(first), Is.Not.EqualTo(EzModSignature.Compute(second)),
                "转换 mod 之间只有 ApplyOrder 相等时才按列表序生效，顺序不同的两组 mod 不能共用一个键");
        }

        [Test]
        public void TestSignatureFollowsSettingValues()
        {
            var a = new ManiaModNtoM { Key = { Value = 8 } };
            var b = new ManiaModNtoM { Key = { Value = 9 } };

            Assert.That(EzModSignature.Compute(new Mod[] { a }), Is.Not.EqualTo(EzModSignature.Compute(new Mod[] { b })));
        }

        [Test]
        public void TestNullSeedResolvesIntoSignatureAndStaysStable()
        {
            // ManiaModCleanColumn 的 Seed 默认是 null，即「尚未掷出」。
            var mod = new ManiaModCleanColumn();

            int unresolved = EzModSignature.Compute(new Mod[] { mod });
            int resolved = EzModSignature.ComputeResolvingSeeds(new Mod[] { mod });

            Assert.That(resolved, Is.Not.EqualTo(unresolved),
                "种子未定与已掷出是两种不同的转换输入，键必须区分");
            Assert.That(EzModSignature.ComputeResolvingSeeds(new Mod[] { mod }), Is.EqualTo(resolved),
                "同一次掷出必须给出稳定的键，否则每次查询都会重新转换");
        }

        [Test]
        public void TestResolvingSeedsLeavesConcreteSeedUntouched()
        {
            var mod = new ManiaModNtoM { Seed = { Value = 4242 } };

            Assert.That(EzModSignature.ComputeResolvingSeeds(new Mod[] { mod }), Is.EqualTo(EzModSignature.Compute(new Mod[] { mod })));
            Assert.That(mod.Seed.Value, Is.EqualTo(4242), "已掷出的种子不得被改写");
        }

        [Test]
        public void TestSnapshotForConversionHandlesSeedlessAndNullMods()
        {
            var seedless = new ManiaModKey7();

            Assert.DoesNotThrow(() => EzModSignature.SnapshotForConversion(new Mod[] { seedless }));
            Assert.DoesNotThrow(() => EzModSignature.SnapshotForConversion(null));
            Assert.That(EzModSignature.SnapshotForConversion(null), Is.Empty);
        }

        /// <summary>
        /// 快照是转换的输入，必须带上「键所依据的那个 seed」：否则克隆体会自己重掷一次，
        /// 于是键对应一个结果、实际转换出另一个结果。
        /// </summary>
        [Test]
        public void TestSnapshotCarriesEffectiveSeedIntoClone()
        {
            var mod = new ManiaModNtoM { Seed = { Value = 4242 } };

            Mod[] snapshot = EzModSignature.SnapshotForConversion(new Mod[] { mod });

            Assert.That(snapshot, Has.Length.EqualTo(1));
            Assert.That(snapshot[0], Is.Not.SameAs(mod), "快照必须是独立实例，转换会往它上面写东西");
            Assert.That(((IHasSeed)snapshot[0]).Seed.Value, Is.EqualTo(4242));
            Assert.That(mod.Seed.Value, Is.EqualTo(4242), "取快照不得改写调用方的 mod");
        }

        [Test]
        public void TestSnapshotResolvesNullSeedConsistentlyWithKey()
        {
            var mod = new ManiaModCleanColumn();

            Mod[] snapshot = EzModSignature.SnapshotForConversion(new Mod[] { mod });

            Assert.That(snapshot[0], Is.Not.SameAs(mod));
            Assert.That(((IHasSeed)snapshot[0]).Seed.Value, Is.Not.Null, "null 种子必须在快照期就解析掉");

            // 写回 live bindable 可能被排到 update 线程（测试/启动早期甚至永远不执行），
            // 所以这里比的是「转换时真正会用的那个数」——EzModSeed.Resolve——而不是 bindable 的当前值。
            Assert.That(((IHasSeed)snapshot[0]).Seed.Value, Is.EqualTo(EzModSeed.Resolve(mod.Seed)),
                "快照与调用方必须看到同一次掷出，否则转换结果无法由键复现");
        }
    }
}
