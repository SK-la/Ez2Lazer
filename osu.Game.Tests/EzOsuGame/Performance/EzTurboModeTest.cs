// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Performance;

namespace osu.Game.Tests.EzOsuGame.Performance
{
    [TestFixture]
    public class EzTurboModeTest
    {
        [Test]
        public void TestOverridesAndRestores()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            osuConfig.SetValue(OsuSetting.DimLevel, 0.7);
            osuConfig.SetValue(OsuSetting.ShowStoryboard, true);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                Assert.That(EzTurboMode.Active, Is.True);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.False);
                // 压制全程生效，选歌与主菜单的项也一并改写。
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Never));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);

                Assert.That(EzTurboMode.Active, Is.False);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.True);
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Sometimes));
                Assert.That(ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot), Is.Empty);
            }
        }

        /// <summary>
        /// 被接管的项在生效期间应置灰，使设置面板显示为不可改，并挡住外部写入。
        /// </summary>
        [Test]
        public void TestManagedSettingsAreLockedWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            osuConfig.SetValue(OsuSetting.DimLevel, 0.7);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                Assert.That(osuConfig.GetBindable<double>(OsuSetting.DimLevel).Disabled, Is.True);
                Assert.Throws<InvalidOperationException>(() => osuConfig.SetValue(OsuSetting.DimLevel, 0.2));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);

                // 解锁必须发生在写回之前，否则还原本身就会抛。
                Assert.That(osuConfig.GetBindable<double>(OsuSetting.DimLevel).Disabled, Is.False);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));
                Assert.DoesNotThrow(() => osuConfig.SetValue(OsuSetting.DimLevel, 0.2));
            }
        }

        /// <summary>
        /// 有运行时写入方的项只压值、不置灰：<see cref="OsuSetting.GameplayLeaderboard"/> 有游玩中可按的快捷键
        /// （<c>HUDOverlay</c> 直接写 bindable），<see cref="OsuSetting.MenuParallaxScale"/> 有一次性配置迁移会写。
        /// 锁上它们会让那些写入抛异常。
        /// </summary>
        [Test]
        public void TestSettingsWithRuntimeWritersStayWritable()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                Assert.That(osuConfig.Get<bool>(OsuSetting.GameplayLeaderboard), Is.False);
                Assert.That(osuConfig.GetBindable<bool>(OsuSetting.GameplayLeaderboard).Disabled, Is.False);
                Assert.DoesNotThrow(() => osuConfig.SetValue(OsuSetting.GameplayLeaderboard, true));

                Assert.That(osuConfig.GetBindable<float>(OsuSetting.MenuParallaxScale).Disabled, Is.False);
                Assert.DoesNotThrow(() => osuConfig.SetValue(OsuSetting.MenuParallaxScale, 1f));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);
            }
        }

        /// <summary>
        /// 列模糊与 Ez 分析都不再走配置压制：前者是皮肤 JSON 的一部分（换皮肤会写回，改成消费点 gate），
        /// 后者只影响选歌流畅度，不值得用功能数据换。
        /// </summary>
        [Test]
        public void TestSettingsOutsideOverrideListAreUntouched()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            ezConfig.SetValue(Ez2Setting.ColumnBlur, 0.3);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                Assert.That(ezConfig.Get<double>(Ez2Setting.ColumnBlur), Is.EqualTo(0.3));
                Assert.That(ezConfig.GetBindable<double>(Ez2Setting.ColumnBlur).Disabled, Is.False);
                Assert.That(ezConfig.Get<bool>(Ez2Setting.EzAnalysisRecEnabled), Is.True);
                Assert.That(ezConfig.Get<bool>(Ez2Setting.EzAnalysisSqliteEnabled), Is.True);

                ezConfig.SetValue(Ez2Setting.TurboMode, false);
            }
        }

        [Test]
        public void TestSnapshotPersistsWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                string snapshot = ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot);

                Assert.That(snapshot, Is.Not.Empty);
                Assert.That(snapshot, Does.Contain(nameof(OsuSetting.DimLevel)));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);
            }
        }

        [Test]
        public void TestStaleSnapshotIsRestoredOnConstruction()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            // 模拟上次在压制生效期间异常退出：磁盘上留着压制值与一份原值快照。
            osuConfig.SetValue(OsuSetting.DimLevel, 1.0);
            osuConfig.SetValue(OsuSetting.ShowStoryboard, false);
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot,
                $"{{\"{nameof(OsuSetting)}.{nameof(OsuSetting.DimLevel)}\":\"0.55\",\"{nameof(OsuSetting)}.{nameof(OsuSetting.ShowStoryboard)}\":\"True\"}}");

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.55));
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.True);
                Assert.That(ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot), Is.Empty);
                Assert.That(EzTurboMode.Active, Is.False);
            }
        }

        [Test]
        public void TestCorruptSnapshotIsDiscardedWithoutThrowing()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            osuConfig.SetValue(OsuSetting.DimLevel, 1.0);
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot, "not json at all");

            Assert.DoesNotThrow(() =>
            {
                using (new EzTurboMode(osuConfig, ezConfig))
                {
                }
            });

            Assert.That(ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot), Is.Empty);
        }

        [Test]
        public void TestDisposeRestoresWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            osuConfig.SetValue(OsuSetting.DimLevel, 0.4);

            var turboMode = new EzTurboMode(osuConfig, ezConfig);
            ezConfig.SetValue(Ez2Setting.TurboMode, true);
            Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));

            turboMode.Dispose();

            Assert.That(EzTurboMode.Active, Is.False);
            Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.4));
            Assert.That(osuConfig.GetBindable<double>(OsuSetting.DimLevel).Disabled, Is.False);
        }
    }
}
