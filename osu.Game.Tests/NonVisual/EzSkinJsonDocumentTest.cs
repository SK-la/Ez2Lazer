// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinJsonDocumentTest
    {
        [Test]
        public void TestParseAndSerializeRoundTrip()
        {
            const string input = """
                                 {
                                   "schemaVersion": 1,
                                   "settings": {
                                     "ColumnWidth": "76",
                                     "ColorSettingsEnabled": "True"
                                   }
                                 }
                                 """;

            var document = EzSkinJsonDocument.Parse(input);

            Assert.That(document.SchemaVersion, Is.EqualTo(1));
            Assert.That(document.Settings["ColumnWidth"], Is.EqualTo("76"));
            Assert.That(document.Settings["ColorSettingsEnabled"], Is.EqualTo("True"));

            var roundTrip = EzSkinJsonDocument.Parse(document.Serialize());
            Assert.That(roundTrip.Settings["ColumnWidth"], Is.EqualTo("76"));
        }

        [Test]
        public void TestUnknownKeysPreservedThroughParse()
        {
            const string input = """
                                 {
                                   "schemaVersion": 1,
                                   "settings": {
                                     "FutureSetting": "1"
                                   }
                                 }
                                 """;

            var document = EzSkinJsonDocument.Parse(input);
            Assert.That(document.Settings.ContainsKey("FutureSetting"), Is.True);
        }
    }

    [TestFixture]
    public class EzSkinJsonBridgeTest
    {
        [Test]
        public void TestCaptureAndApplyCatalogSettings()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);

            config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 88;
            config.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled).Value = true;

            var captured = EzSkinJsonBridge.Capture(config);
            Assert.That(double.Parse(captured.Settings[nameof(Ez2Setting.ColumnWidth)], CultureInfo.InvariantCulture), Is.EqualTo(88));

            config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 40;
            EzSkinJsonBridge.Apply(captured, config);

            Assert.That(config.GetBindable<double>(Ez2Setting.ColumnWidth).Value, Is.EqualTo(88));

            host.Exit();
        }
    }
}
