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
        /// 可安全置灰的项在生效期间应 Disabled；存在后建 SliderBar 绑定或运行时写入方的项只压值、不置灰。
        /// </summary>
        [Test]
        public void TestManagedSettingsAreLockedWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);

            osuConfig.SetValue(OsuSetting.DimLevel, 0.7);
            osuConfig.SetValue(OsuSetting.BlurLevel, 0.4);
            osuConfig.SetValue(OsuSetting.ShowStoryboard, true);

            using (new EzTurboMode(osuConfig, ezConfig))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                // Checkbox 等只 BindTo、不写 Default 的项可以置灰。
                Assert.That(osuConfig.GetBindable<bool>(OsuSetting.ShowStoryboard).Disabled, Is.True);
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.False);
                Assert.Throws<InvalidOperationException>(() => osuConfig.SetValue(OsuSetting.ShowStoryboard, true));

                // Dim/Blur 经 PlayerLoader VisualSettings → framework SliderBar 赋 Current；
                // 源若 Disabled 会在写 Default 时崩，故只压值不置灰。
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));
                Assert.That(osuConfig.GetBindable<double>(OsuSetting.DimLevel).Disabled, Is.False);
                Assert.DoesNotThrow(() => osuConfig.SetValue(OsuSetting.DimLevel, 0.2));
                Assert.That(osuConfig.GetBindable<double>(OsuSetting.DimLevel).Default, Is.EqualTo(0.7).Within(1e-6));

                Assert.That(osuConfig.Get<double>(OsuSetting.BlurLevel), Is.EqualTo(0.0));
                Assert.That(osuConfig.GetBindable<double>(OsuSetting.BlurLevel).Disabled, Is.False);
                Assert.DoesNotThrow(() => osuConfig.SetValue(OsuSetting.BlurLevel, 0.3));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);

                Assert.That(osuConfig.GetBindable<bool>(OsuSetting.ShowStoryboard).Disabled, Is.False);
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.True);
                // 生效期间用户改过的滑条值按快照还原，不保留手动改动。
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));
                Assert.That(osuConfig.Get<double>(OsuSetting.BlurLevel), Is.EqualTo(0.4));
            }
        }

        /// <summary>
        /// 有运行时写入方或后建 SliderBar 绑定方的项只压值、不置灰：
        /// <see cref="OsuSetting.GameplayLeaderboard"/> 有游玩中快捷键，
        /// <see cref="OsuSetting.MenuParallaxScale"/> 有一次性配置迁移，
        /// <see cref="OsuSetting.DimLevel"/> / <see cref="OsuSetting.BlurLevel"/> 见 <see cref="TestManagedSettingsAreLockedWhileActive"/>。
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
