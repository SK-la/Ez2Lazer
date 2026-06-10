// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorNoteEditSnapshotTest
    {
        [Test]
        public void TestCaptureApplyRoundTrip()
        {
            var session = new EzSkinEditorNoteEditSession
            {
                Ruleset = { Value = new ManiaRuleset().RulesetInfo },
                Part =
                {
                    Value = EzSkinEditorNotePart.HoldBody
                },
                VariantId =
                {
                    Value = "2"
                },
                NoteColour =
                {
                    Value = Colour4.Cyan
                },
                Width =
                {
                    Value = 120
                },
                Height =
                {
                    Value = 80
                },
                ExportName =
                {
                    Value = "test-note"
                }
            };

            var snapshot = new EzSkinEditorNoteEditSnapshot();
            snapshot.CaptureFrom(session);

            session.Part.Value = EzSkinEditorNotePart.Note;
            session.VariantId.Value = "1";
            session.NoteColour.Value = Colour4.Red;
            session.Width.Value = 50;
            session.Height.Value = 40;
            session.ExportName.Value = "changed";

            snapshot.ApplyTo(session);

            Assert.That(session.Part.Value, Is.EqualTo(EzSkinEditorNotePart.HoldBody));
            Assert.That(session.VariantId.Value, Is.EqualTo("2"));
            Assert.That(session.NoteColour.Value, Is.EqualTo(Colour4.Cyan));
            Assert.That(session.Width.Value, Is.EqualTo(120));
            Assert.That(session.Height.Value, Is.EqualTo(80));
            Assert.That(session.ExportName.Value, Is.EqualTo("test-note"));
        }
    }
}
