// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorComparisonSnapshotTest : ImportTest
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

        [Test]
        public void TestSkinIniDraftCaptureApplyWhenUnsupported()
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host);
                var config = new Ez2ConfigManager(host.Storage);
                var session = new EzSkinIniSession(osu.Dependencies.Get<SkinManager>());

                var snapshot = new EzSkinEditorComparisonSnapshot();
                snapshot.CaptureFrom(config, session);

                Assert.That(snapshot.SkinIniDraftText, Is.Null);
            }
            finally
            {
                host.Exit();
            }
        }

        [Test]
        public void TestSkinIniDraftCaptureApplyRoundTrip()
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host);
                var config = new Ez2ConfigManager(host.Storage);
                var skinManager = osu.Dependencies.Get<SkinManager>();
                var session = new EzSkinIniSession(skinManager);

                const string modified = "[General]\nName: Modified Skin\n";

                var imported = skinManager.Import(new ImportTask(TestResources.OpenResource(@"Archives/modified-ezSkin.osk"), "modified-ezSkin.osk")).GetResultSafely();
                skinManager.CurrentSkinInfo.Value = imported;
                session.LoadFromSkin(imported);

                Assert.That(session.IsSupported, Is.True);

                string originalDraft = session.DraftText;
                var snapshot = new EzSkinEditorComparisonSnapshot();
                snapshot.CaptureFrom(config, session);

                Assert.That(snapshot.SkinIniDraftText, Is.EqualTo(originalDraft));

                session.SetDraftText(modified);
                Assert.That(session.DraftText, Is.EqualTo(modified));

                snapshot.ApplyTo(config, session);
                Assert.That(session.DraftText, Is.EqualTo(originalDraft));
            }
            finally
            {
                host.Exit();
            }
        }
    }
}
