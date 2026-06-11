// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class ScriptedSkinSupportTest
    {
        [Test]
        public void TestCanUseRealmExternalEditIsFalseForScriptedSkin()
        {
            var skinInfo = new SkinInfo("TestScript", "author", typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo())
            {
                Hash = "TestScript",
            };

            Assert.That(ScriptedSkinSupport.IsScriptedSkin(skinInfo), Is.True);
            Assert.That(ScriptedSkinSupport.CanUseRealmExternalEdit(skinInfo, isManaged: false), Is.False);
            Assert.That(ScriptedSkinSupport.CanUseRealmExternalEdit(skinInfo.ToLiveUnmanaged()), Is.False);
        }

        [Test]
        public void TestSaveLayoutToScriptDirectoryWritesJson()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "ScriptedSkinSupportTest", Guid.NewGuid().ToString("N"));
            string scriptDirectory = Path.Combine(basePath, "ScriptedSkin", "LayoutTest");
            Directory.CreateDirectory(scriptDirectory);
            File.WriteAllText(Path.Combine(scriptDirectory, "LayoutTestSkin.csx"), "// test");

            var skinInfo = new SkinInfo
            {
                Name = "LayoutTest",
                Hash = "LayoutTest",
                InstantiationInfo = typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo(),
            };

            var skin = new TrianglesSkin(skinInfo, resources: null!);
            skin.LayoutInfos[GlobalSkinnableContainers.MainHUDComponents] = new SkinLayoutInfo();

            bool changed = ScriptedSkinSupport.SaveLayoutToScriptDirectory(skin, basePath);

            Assert.That(changed, Is.True);
            Assert.That(File.Exists(Path.Combine(scriptDirectory, "MainHUDComponents.json")), Is.True);
        }
    }
}
