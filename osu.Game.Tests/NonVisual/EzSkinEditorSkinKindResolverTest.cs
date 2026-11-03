// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorSkinKindResolverTest
    {
        [Test]
        public void TestResolveFromSkinInfoInstantiation()
        {
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(EzStyleProSkin.CreateInfo()), Is.EqualTo(EzSkinEditorSkinKind.EzStylePro));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(Ez2Skin.CreateInfo()), Is.EqualTo(EzSkinEditorSkinKind.Ez2));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(SbISkin.CreateInfo()), Is.EqualTo(EzSkinEditorSkinKind.SbI));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(ArgonSkin.CreateInfo()), Is.EqualTo(EzSkinEditorSkinKind.None));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve((SkinInfo?)null), Is.EqualTo(EzSkinEditorSkinKind.None));
        }

        [Test]
        public void TestRegistryHasOneProviderPerKind()
        {
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinEditorSkinKind.EzStylePro), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinEditorSkinKind.Ez2), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinEditorSkinKind.SbI), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinEditorSkinKind.Scripted), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinEditorSkinKind.None), Is.Empty);
        }
    }
}
