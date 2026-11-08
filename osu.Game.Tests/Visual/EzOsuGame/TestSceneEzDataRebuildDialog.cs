// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    /// <summary>
    /// The maintenance dialog must lay out and must actually draw the dependency grouping: tight inside the chart
    /// chain, wide between independent systems.
    /// </summary>
    public partial class TestSceneEzDataRebuildDialog : OsuTestScene
    {
        private EzDataRebuildDialog dialog = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create dialog", () => Child = dialog = new EzDataRebuildDialog());
            AddStep("show", () => dialog.Show());
        }

        [Test]
        public void TestEveryCatalogNodeIsDrawn()
        {
            AddUntilStep("all node cards visible", () =>
                dialog.ChildrenOfType<EzDataRebuildDialog.RebuildNodeCard>()
                      .Select(c => c.NodeAction)
                      .OrderBy(a => a)
                      .SequenceEqual(EzDataRebuildCatalog.All.Select(n => n.Action).OrderBy(a => a)));
        }

        [Test]
        public void TestSpacingEncodesTheDependencyGraph()
        {
            AddUntilStep("all node cards visible", () => dialog.ChildrenOfType<EzDataRebuildDialog.RebuildNodeCard>().Count()
                                                         == EzDataRebuildCatalog.All.Count);

            AddStep("measure gaps", () =>
            {
                var cards = dialog.ChildrenOfType<EzDataRebuildDialog.RebuildNodeCard>().ToList();

                // A card's Y is its margin box top; the margin is the gap inside that box.
                float gapAfter(EzDataRebuildAction action)
                {
                    var current = cards.Single(c => c.NodeAction == action);
                    var next = cards.ElementAt(cards.IndexOf(current) + 1);

                    float currentContentBottom = current.Y + current.Margin.Top + current.DrawHeight;
                    float nextContentTop = next.Y + next.Margin.Top;

                    return nextContentTop - currentContentBottom;
                }

                float msdToCsi = gapAfter(EzDataRebuildAction.RealmChartMsd);
                float csiToDan = gapAfter(EzDataRebuildAction.RealmChartCsi);
                float danToPpp = gapAfter(EzDataRebuildAction.RealmChartDan);

                Assert.That(msdToCsi, Is.EqualTo(EzDataRebuildCatalog.CHAIN_GAP), "Mina feeds CSI");
                Assert.That(csiToDan, Is.EqualTo(EzDataRebuildCatalog.CHAIN_GAP), "CSI feeds Dan");
                Assert.That(danToPpp, Is.EqualTo(EzDataRebuildCatalog.INDEPENDENT_GAP), "PP+ does not read Dan");
            });
        }

        [Test]
        public void TestSelectingANodeMarksIt()
        {
            AddStep("select CSI", () => card(EzDataRebuildAction.RealmChartCsi).TriggerClick());

            AddAssert("footer names the selection", () =>
                dialog.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("CSI")));
        }

        [Test]
        public void TestClickingASelectedNodeDeselectsIt()
        {
            AddStep("select CSI", () => card(EzDataRebuildAction.RealmChartCsi).TriggerClick());
            AddAssert("CSI selected", () => dialog.SelectedActions.Single() == EzDataRebuildAction.RealmChartCsi);

            AddStep("select CSI again", () => card(EzDataRebuildAction.RealmChartCsi).TriggerClick());
            AddAssert("selection cleared", () => dialog.SelectedActions.Count == 0);
        }

        [Test]
        public void TestSelectionAcceptsMultipleNodes()
        {
            AddStep("select Mina", () => card(EzDataRebuildAction.RealmChartMsd).TriggerClick());
            AddStep("select CSI", () => card(EzDataRebuildAction.RealmChartCsi).TriggerClick());

            AddAssert("both selected", () => dialog.SelectedActions.OrderBy(a => a)
                                                   .SequenceEqual(new[] { EzDataRebuildAction.RealmChartMsd, EzDataRebuildAction.RealmChartCsi }.OrderBy(a => a)));

            AddStep("deselect Mina", () => card(EzDataRebuildAction.RealmChartMsd).TriggerClick());
            AddAssert("CSI stays selected", () => dialog.SelectedActions.Single() == EzDataRebuildAction.RealmChartCsi);
        }

        [Test]
        public void TestDependentNodeExplainsItsInput()
        {
            AddAssert("CSI mentions its input", () => !LocalisableString.IsNullOrEmpty(card(EzDataRebuildAction.RealmChartCsi).TooltipText));
            AddAssert("Mina stands alone", () => LocalisableString.IsNullOrEmpty(card(EzDataRebuildAction.RealmChartMsd).TooltipText));
        }

        private EzDataRebuildDialog.RebuildNodeCard card(EzDataRebuildAction action) =>
            dialog.ChildrenOfType<EzDataRebuildDialog.RebuildNodeCard>().Single(c => c.NodeAction == action);
    }
}
