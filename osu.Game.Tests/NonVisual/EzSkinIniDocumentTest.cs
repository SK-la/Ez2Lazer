// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Edit;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinIniDocumentTest
    {
        [Test]
        public void TestParseAndSerializeGeneralSection()
        {
            const string input = """
                                 [General]
                                 Name: Ez Skin
                                 Author: Tester
                                 Version: 2.7
                                 """;

            var document = EzSkinIniDocument.Parse(input);

            Assert.That(document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name"), Is.EqualTo("Ez Skin"));
            Assert.That(document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Author"), Is.EqualTo("Tester"));
            Assert.That(document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Version"), Is.EqualTo("2.7"));

            string output = document.Serialize();
            var roundTrip = EzSkinIniDocument.Parse(output);

            Assert.That(roundTrip.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name"), Is.EqualTo("Ez Skin"));
            Assert.That(roundTrip.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Version"), Is.EqualTo("2.7"));
        }

        [Test]
        public void TestSetValueCreatesSectionAndPreservesMania()
        {
            const string input = """
                                 [General]
                                 Name: Before
                                 [Mania]
                                 ColumnWidth: 34
                                 // comment line
                                 """;

            var document = EzSkinIniDocument.Parse(input);
            document.SetValue(EzSkinIniDocument.GENERAL_SECTION, "Name", "After");
            document.SetValue(EzSkinIniDocument.MANIA_SECTION, "HitPosition", "480");

            Assert.That(document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name"), Is.EqualTo("After"));
            Assert.That(document.GetValue(EzSkinIniDocument.MANIA_SECTION, "ColumnWidth"), Is.EqualTo("34"));
            Assert.That(document.GetValue(EzSkinIniDocument.MANIA_SECTION, "HitPosition"), Is.EqualTo("480"));

            string output = document.Serialize();
            Assert.That(output, Does.Contain("[Mania]"));
            Assert.That(output, Does.Contain("ColumnWidth: 34"));
            Assert.That(output, Does.Contain("// comment line"));
            Assert.That(output, Does.Contain("Name: After"));
        }

        [Test]
        public void TestColoursRoundTrip()
        {
            const string input = """
                                 [Colours]
                                 Combo1: 255,192,0
                                 MenuGlow: 18,124,255,128
                                 """;

            var document = EzSkinIniDocument.Parse(input);

            Assert.That(document.TryGetColourValue("Combo1", out var combo), Is.True);
            Assert.That(combo.R, Is.EqualTo(255 / 255f).Within(0.01f));
            Assert.That(combo.G, Is.EqualTo(192 / 255f).Within(0.01f));
            Assert.That(combo.B, Is.EqualTo(0f).Within(0.01f));

            document.SetColourValue("Combo2", new Colour4(0, 202, 0, 255));

            string output = document.Serialize();
            var roundTrip = EzSkinIniDocument.Parse(output);

            Assert.That(roundTrip.GetColourValue("Combo1"), Is.EqualTo("255,192,0"));
            Assert.That(roundTrip.GetColourValue("Combo2"), Is.EqualTo("0,202,0"));
            Assert.That(roundTrip.GetColourValue("MenuGlow"), Is.EqualTo("18,124,255,128"));
        }

        [Test]
        public void TestManiaBlocksAreIsolated()
        {
            const string input = """
                                 [Mania]
                                 Keys: 4
                                 ColumnWidth: 10,10,10,10
                                 HitPosition: 470
                                 Keys: 2
                                 ColumnWidth: 20,20
                                 HitPosition: 460
                                 """;

            var document = EzSkinIniDocument.Parse(input);

            Assert.That(document.GetManiaKeys(), Is.EquivalentTo(new[] { 4, 2 }));
            Assert.That(document.GetManiaValue(4, "ColumnWidth"), Is.EqualTo("10,10,10,10"));
            Assert.That(document.GetManiaValue(2, "ColumnWidth"), Is.EqualTo("20,20"));

            document.SetManiaValue(4, "ColumnWidth", "16,16,16,16");

            Assert.That(document.GetManiaValue(4, "ColumnWidth"), Is.EqualTo("16,16,16,16"));
            Assert.That(document.GetManiaValue(2, "ColumnWidth"), Is.EqualTo("20,20"));
        }

        [Test]
        public void TestEnsureManiaBlockCreatesKeysLine()
        {
            var document = EzSkinIniDocument.Parse(string.Empty);
            document.EnsureManiaBlock(7);
            document.SetManiaValue(7, "HitPosition", "400");

            string output = document.Serialize();

            Assert.That(output, Does.Contain("[Mania]"));
            Assert.That(output, Does.Contain("Keys: 7"));
            Assert.That(output, Does.Contain("HitPosition: 400"));
        }
    }
}
