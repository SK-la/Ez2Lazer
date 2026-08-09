// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Performance;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.EzOsuGame.Performance
{
    [TestFixture]
    public class EzTurboModeTest
    {
        [Test]
        public void TestGlobalModeOverridesAndRestores()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);
            var playingState = new Bindable<LocalUserPlayingState>();

            osuConfig.SetValue(OsuSetting.DimLevel, 0.7);
            osuConfig.SetValue(OsuSetting.ShowStoryboard, true);
            ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, false);

            using (new EzTurboMode(osuConfig, ezConfig, playingState))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                Assert.That(EzTurboMode.Active, Is.True);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.False);
                // 仅全局模式的项也应生效。
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Never));

                ezConfig.SetValue(Ez2Setting.TurboMode, false);

                Assert.That(EzTurboMode.Active, Is.False);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));
                Assert.That(osuConfig.Get<bool>(OsuSetting.ShowStoryboard), Is.True);
                Assert.That(ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot), Is.Empty);
            }
        }

        [Test]
        public void TestGameplayOnlyModeFollowsPlayingState()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);
            var playingState = new Bindable<LocalUserPlayingState>();

            osuConfig.SetValue(OsuSetting.DimLevel, 0.7);
            ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, true);

            using (new EzTurboMode(osuConfig, ezConfig, playingState))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);

                // 总开关已开，但还没进入游玩，不应压制。
                Assert.That(EzTurboMode.Active, Is.False);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));

                playingState.Value = LocalUserPlayingState.Playing;

                Assert.That(EzTurboMode.Active, Is.True);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));
                // 仅全局模式的项不应在游玩压制中被改写。
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Sometimes));

                // 休息段仍算游玩，不应来回还原造成抖动。
                playingState.Value = LocalUserPlayingState.Break;
                Assert.That(EzTurboMode.Active, Is.True);

                playingState.Value = LocalUserPlayingState.NotPlaying;

                Assert.That(EzTurboMode.Active, Is.False);
                Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7));
            }
        }

        [Test]
        public void TestSnapshotPersistsWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);
            var playingState = new Bindable<LocalUserPlayingState>();

            ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, false);

            using (new EzTurboMode(osuConfig, ezConfig, playingState))
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
            var playingState = new Bindable<LocalUserPlayingState>();

            // 模拟上次在压制生效期间异常退出：磁盘上留着压制值与一份原值快照。
            osuConfig.SetValue(OsuSetting.DimLevel, 1.0);
            osuConfig.SetValue(OsuSetting.ShowStoryboard, false);
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot,
                $"{{\"{nameof(OsuSetting)}.{nameof(OsuSetting.DimLevel)}\":\"0.55\",\"{nameof(OsuSetting)}.{nameof(OsuSetting.ShowStoryboard)}\":\"True\"}}");

            using (new EzTurboMode(osuConfig, ezConfig, playingState))
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
            var playingState = new Bindable<LocalUserPlayingState>();

            osuConfig.SetValue(OsuSetting.DimLevel, 1.0);
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot, "not json at all");

            Assert.DoesNotThrow(() =>
            {
                using (new EzTurboMode(osuConfig, ezConfig, playingState))
                {
                }
            });

            Assert.That(ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot), Is.Empty);
        }

        [Test]
        public void TestSwitchingToGameplayOnlyWhileActiveRestoresGlobalOverrides()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);
            var playingState = new Bindable<LocalUserPlayingState>();

            ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, false);

            using (new EzTurboMode(osuConfig, ezConfig, playingState))
            {
                ezConfig.SetValue(Ez2Setting.TurboMode, true);
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Never));

                // 切成仅游玩中生效、且当前不在游玩：压制应整体撤销，含仅全局那一组。
                ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, true);

                Assert.That(EzTurboMode.Active, Is.False);
                Assert.That(osuConfig.Get<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode), Is.EqualTo(SeasonalBackgroundMode.Sometimes));
            }
        }

        [Test]
        public void TestDisposeRestoresWhileActive()
        {
            using var storage = new TemporaryNativeStorage($"ez-turbo-{Guid.NewGuid()}");
            var osuConfig = new OsuConfigManager(storage);
            var ezConfig = new Ez2ConfigManager(storage);
            var playingState = new Bindable<LocalUserPlayingState>();

            osuConfig.SetValue(OsuSetting.DimLevel, 0.4);
            ezConfig.SetValue(Ez2Setting.TurboModeGameplayOnly, false);

            var turboMode = new EzTurboMode(osuConfig, ezConfig, playingState);
            ezConfig.SetValue(Ez2Setting.TurboMode, true);
            Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(1.0));

            turboMode.Dispose();

            Assert.That(EzTurboMode.Active, Is.False);
            Assert.That(osuConfig.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.4));
        }
    }
}
