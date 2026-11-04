// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.EzOsuGame.Layout;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzBeatmapEditorSettingsStoreTest
    {
        [Test]
        public void TestBeatLineColoursRoundTripInBeatmapEditorJson()
        {
            using var storage = new TemporaryNativeStorage("ez-beatmap-editor-settings");
            var store = new EzConfigStore(storage);
            store.BeatmapEditor.Colouring.BeatLines.GetBindable(4).Value = Colour4.FromHex("#AABBCC");

            store.SaveBeatmapEditor();

            var loaded = new EzConfigStore(storage).BeatmapEditor.Colouring.BeatLines;

            Assert.That(loaded.GetBindable(4).Value.ToHex(), Is.EqualTo("#AABBCC"));
        }

        [Test]
        public void TestDoesNotUseLegacyPaletteFilename()
        {
            using var storage = new TemporaryNativeStorage("ez-beatmap-editor-filename");
            var configStorage = storage.GetStorageForDirectory(EzModifyPath.CONFIG_ROOT_PATH);
            var store = new EzConfigStore(storage);
            store.BeatmapEditor.Colouring.BeatLines.GetBindable(1).Value = Colour4.FromHex("#112233");
            store.SaveBeatmapEditor();

            Assert.That(configStorage.Exists(BeatmapEditorSettings.FILENAME));
            Assert.That(configStorage.Exists("ManiaEditorBeatLinePalette.json"), Is.False);
        }
    }
}
