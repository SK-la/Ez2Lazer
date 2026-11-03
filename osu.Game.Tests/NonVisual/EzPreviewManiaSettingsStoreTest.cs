// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Overlays.Preview;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzPreviewManiaSettingsStoreTest
    {
        [Test]
        public void TestRoundTripsColoursAndThickness()
        {
            using var storage = new TemporaryNativeStorage("ez-full-map-preview-maniaSettings");
            var store = new EzConfigStore(storage);
            var settings = store.PreviewManiaSettings;
            settings.NoteColour.Value = Colour4.FromHex("#112233");
            settings.HoldHeadColour.Value = Colour4.FromHex("#445566");
            settings.HoldBodyColour.Value = Colour4.FromHex("#778899");
            settings.ThicknessScale.Value = 2.5f;
            settings.LaneBackgroundColour.Value = Colour4.FromHex("#0A0A14CC");

            store.Save();

            var loaded = new EzConfigStore(storage).PreviewManiaSettings;

            Assert.That(loaded.NoteColour.Value.ToHex(), Is.EqualTo("#112233"));
            Assert.That(loaded.HoldHeadColour.Value.ToHex(), Is.EqualTo("#445566"));
            Assert.That(loaded.HoldBodyColour.Value.ToHex(), Is.EqualTo("#778899"));
            Assert.That(loaded.ThicknessScale.Value, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(loaded.LaneBackgroundColour.Value.ToHex(), Is.EqualTo("#0A0A14CC"));
        }

        [Test]
        public void TestMissingFileUsesDefaults()
        {
            using var storage = new TemporaryNativeStorage("ez-full-map-preview-maniaSettings-missing");
            var loaded = new EzConfigStore(storage).PreviewManiaSettings;

            Assert.That(loaded.NoteColour.Value, Is.EqualTo(EzPreviewManiaSettings.DEFAULT_NOTE_COLOUR));
            Assert.That(loaded.ThicknessScale.Value, Is.EqualTo(EzPreviewManiaSettings.DEFAULT_THICKNESS_SCALE));
        }

        [Test]
        public void TestThicknessClampedOnLoad()
        {
            using var storage = new TemporaryNativeStorage("ez-full-map-preview-maniaSettings-clamp");
            var store = new EzConfigStore(storage);
            var settings = store.PreviewManiaSettings;
            settings.ThicknessScale.Value = 99f;
            store.Save();

            var loaded = new EzConfigStore(storage).PreviewManiaSettings;

            Assert.That(loaded.ThicknessScale.Value, Is.LessThanOrEqualTo(EzPreviewManiaSettings.MAX_THICKNESS_SCALE));
        }
    }
}
