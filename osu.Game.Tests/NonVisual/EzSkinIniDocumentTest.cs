// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
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
    }
}
