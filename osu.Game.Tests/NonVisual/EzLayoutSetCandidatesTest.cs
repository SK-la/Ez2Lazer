// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Layout;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzLayoutSetCandidatesTest
    {
        [Test]
        public void TestDiscoveryFindsLayoutAndPrefixedFolders()
        {
            using var storage = new TemporaryNativeStorage("ez-layout-set-candidates");

            Directory.CreateDirectory(Path.Combine(storage.GetFullPath(EzModifyPath.CONFIG_ROOT_PATH), "layout-O2jam"));
            Directory.CreateDirectory(Path.Combine(storage.GetFullPath(EzModifyPath.CONFIG_ROOT_PATH), "not-layout"));
            Directory.CreateDirectory(Path.Combine(storage.GetFullPath(EzModifyPath.CONFIG_ROOT_PATH), "layout"));

            EzLayoutSetCandidates.Refresh(storage);

            Assert.That(EzLayoutSetCandidates.Items, Does.Contain("layout"));
            Assert.That(EzLayoutSetCandidates.Items, Does.Contain("layout-O2jam"));
            Assert.That(EzLayoutSetCandidates.Items, Has.No.Member("not-layout"));
            Assert.That(EzLayoutSetCandidates.Items[0], Is.EqualTo("layout"));
        }

        [Test]
        public void TestNormalizeMapsThemeSuffixToDirectoryName()
        {
            Assert.That(EzLayoutSetNames.Normalize("O2jam"), Is.EqualTo("layout-O2jam"));
            Assert.That(EzLayoutSetNames.Normalize("layout-O2jam"), Is.EqualTo("layout-O2jam"));
            Assert.That(EzLayoutSetNames.Normalize(null), Is.EqualTo("layout"));
            Assert.That(EzLayoutSetNames.DirectoryNameForThemeSuffix("AIR"), Is.EqualTo("layout-AIR"));
        }
    }
}
