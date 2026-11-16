// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzColumnResourceLookupTest
    {
        [Test]
        public void TestSharedRedAffixMapsPerColumnType()
        {
            Assert.That(EzColumnResourceLookup.EzProNoteTexturePrefix(EzColumnType.S), Is.EqualTo("red"));
            Assert.That(EzColumnResourceLookup.EzProNoteTexturePrefix(EzColumnType.E), Is.EqualTo("red"));
            Assert.That(EzColumnResourceLookup.EzProNoteTexturePrefix(EzColumnType.A), Is.Not.EqualTo(EzColumnResourceLookup.EzProNoteTexturePrefix(EzColumnType.B)));
        }
    }
}