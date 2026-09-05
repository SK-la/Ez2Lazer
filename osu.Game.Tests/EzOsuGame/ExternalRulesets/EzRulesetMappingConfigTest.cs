// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.ExternalRulesets;

namespace osu.Game.Tests.EzOsuGame.ExternalRulesets
{
    [TestFixture]
    public class EzRulesetMappingConfigTest
    {
        [Test]
        public void TestSaveWritesHeaderAndExplicitOnlyDefaults()
        {
            using var host = new TemporaryNativeStorage("ez-ruleset-mapping-test");
            var storage = host.GetStorageForDirectory(".");

            var config = new EzRulesetMappingConfig();
            config.EnsureDefaults(new[]
            {
                new DiscoveredExternalRuleset("no-id", "No ID Ruleset", -1),
                new DiscoveredExternalRuleset("diva", "DIVA", 4),
            });

            config.Save(storage);

            string text = File.ReadAllText(storage.GetFullPath(EzRulesetMappingConfig.FILENAME));

            Assert.That(text, Does.Contain(EzRulesetMappingConfig.HEADER_LINE1));
            Assert.That(text, Does.Contain(EzRulesetMappingConfig.HEADER_LINE2));
            Assert.That(text, Does.Contain("[no-id]"));
            Assert.That(text, Does.Contain("[diva]"));
            Assert.That(text, Does.Contain("OnlineID=4"));
            Assert.That(text, Does.Not.Contain("[no-id]\r\nEnabled=1\r\nOnlineID="));
        }

        [Test]
        public void TestLoadRoundTrip()
        {
            using var host = new TemporaryNativeStorage("ez-ruleset-mapping-roundtrip");
            var storage = host.GetStorageForDirectory(".");

            var original = new EzRulesetMappingConfig();
            var diva = original.GetOrAdd("diva");
            diva.Enabled = true;
            diva.OnlineID = 7;
            diva.Order = 2;
            original.Save(storage);

            var loaded = EzRulesetMappingConfig.Load(storage);
            var entry = loaded.GetOrAdd("diva");

            Assert.That(entry.Enabled, Is.True);
            Assert.That(entry.OnlineID, Is.EqualTo(7));
            Assert.That(entry.Order, Is.EqualTo(2));
        }
    }
}
