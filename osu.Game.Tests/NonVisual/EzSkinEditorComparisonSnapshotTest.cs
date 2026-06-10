// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorComparisonSnapshotTest
    {
        [Test]
        public void TestCaptureApplyAndDefaultSync()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);

            double originalWidth = config.Get<double>(Ez2Setting.ColumnWidth);
            double originalDefault = config.GetBindable<double>(Ez2Setting.ColumnWidth).Default;

            try
            {
                var snapshot = new EzSkinEditorComparisonSnapshot();
                snapshot.CaptureFrom(config);

                config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = originalWidth + 25;
                snapshot.SyncBindableDefaults(config);

                Assert.That(config.GetBindable<double>(Ez2Setting.ColumnWidth).Default, Is.EqualTo(originalWidth));

                snapshot.ApplyTo(config);
                Assert.That(config.Get<double>(Ez2Setting.ColumnWidth), Is.EqualTo(originalWidth));
            }
            finally
            {
                config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = originalWidth;
                config.GetBindable<double>(Ez2Setting.ColumnWidth).Default = originalDefault;
            }
        }
    }
}
