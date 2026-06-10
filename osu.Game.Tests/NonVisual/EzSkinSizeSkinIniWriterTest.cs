// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinSizeSkinIniWriterTest
    {
        [Test]
        public void TestFormatColumnWidthArrayUsesPerColumnWidths()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);

            const int key_mode = 4;
            config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 40;
            config.GetBindable<double>(Ez2Setting.SpecialFactor).Value = 2;
            config.SetColumnType(key_mode, 1, EzColumnType.S);

            string formatted = EzSkinLegacyManiaIniFormat.FormatColumnWidthArray(key_mode, config);
            float[] parts = formatted.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();

            Assert.That(parts, Has.Length.EqualTo(key_mode));
            Assert.That(parts[0], Is.EqualTo(40 / LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR).Within(0.001));
            Assert.That(parts[1], Is.EqualTo(80 / LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR).Within(0.001));

            host.Exit();
        }

        [Test]
        public void TestFormatHitPositionMatchesLegacyDecoderSemantics()
        {
            const float ez_hit_position = 180f;
            string legacy = EzSkinLegacyManiaIniFormat.FormatHitPosition(ez_hit_position);

            float parsedLegacy = float.Parse(legacy, CultureInfo.InvariantCulture);
            float roundTripEz = (480 - parsedLegacy) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;

            Assert.That(roundTripEz, Is.EqualTo(ez_hit_position).Within(0.01));
        }

        [Test]
        public void TestFormatWidthForNoteHeightScaleUsesNoteHeightScaleToWidth()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);

            const int key_mode = 4;
            config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 64;
            config.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth).Value = 2;

            float columnWidth = config.GetColumnWidth(key_mode, 0);
            float expectedEzHeight = columnWidth * 0.5f * 2f;
            string formatted = EzSkinLegacyManiaIniFormat.FormatWidthForNoteHeightScale(key_mode, config);

            Assert.That(float.Parse(formatted, CultureInfo.InvariantCulture),
                Is.EqualTo(expectedEzHeight / LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR).Within(0.001));

            host.Exit();
        }

        [Test]
        public void TestWriteManiaSizeSettingsToDocument()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);

            const int key_mode = 4;
            config.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 50;
            config.GetBindable<double>(Ez2Setting.HitPosition).Value = 200;
            config.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth).Value = 1.5;

            var document = EzSkinIniDocument.Parse(string.Empty);
            EzSkinSizeSkinIniWriter.WriteManiaSizeSettingsToDocument(key_mode, config, document);

            Assert.That(document.GetManiaValue(key_mode, "ColumnWidth"), Is.EqualTo(EzSkinLegacyManiaIniFormat.FormatColumnWidthArray(key_mode, config)));
            Assert.That(document.GetManiaValue(key_mode, "HitPosition"), Is.EqualTo(EzSkinLegacyManiaIniFormat.FormatHitPosition(200)));
            Assert.That(document.GetManiaValue(key_mode, "WidthForNoteHeightScale"), Is.EqualTo(EzSkinLegacyManiaIniFormat.FormatWidthForNoteHeightScale(key_mode, config)));

            host.Exit();
        }
    }
}
