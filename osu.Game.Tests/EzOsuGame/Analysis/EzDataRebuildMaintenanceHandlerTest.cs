// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Moq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzDataRebuildMaintenanceHandlerTest
    {
        [TestCase(EzDataRebuildDispatchResult.UnavailableProcessor)]
        [TestCase(EzDataRebuildDispatchResult.AlreadyRunning)]
        [TestCase(EzDataRebuildDispatchResult.SqliteDisabled)]
        public void TestGetErrorMessageReturnsMessageForFailureResults(EzDataRebuildDispatchResult result)
        {
            Assert.That(EzDataRebuildMaintenanceHandler.GetErrorMessage(result).ToString(), Is.Not.Empty);
        }

        [Test]
        public void TestCanExecuteReflectsTheActionsOwnStore()
        {
            var dispatcher = new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.Queued, null, null);
            var handler = new EzDataRebuildMaintenanceHandler(dispatcher, new Mock<IDialogOverlay>().Object, notifications: null);

            Assert.That(handler.CanExecute(EzDataRebuildAction.RealmAll), Is.True);
            Assert.That(handler.CanExecute(EzDataRebuildAction.RealmChartCsi), Is.True);
            Assert.That(handler.CanExecute(EzDataRebuildAction.SqliteMain), Is.False);
        }

        [Test]
        public void TestBackfillDispatchesWithoutADialog()
        {
            EzDataRebuildAction? dispatched = null;

            var handler = new EzDataRebuildMaintenanceHandler(
                new EzDataRebuildDispatcher((_, _) =>
                {
                    dispatched = EzDataRebuildAction.RealmChartMsd;
                    return EzDataRebuildDispatchResult.Queued;
                }, null, null),
                dialogOverlay: null,
                notifications: null);

            Assert.That(handler.RequestBackfill(EzDataRebuildAction.RealmChartMsd, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(dispatched, Is.EqualTo(EzDataRebuildAction.RealmChartMsd));
        }

        [Test]
        public void TestForceRebuildSelectionUsesOneConfirmation()
        {
            var dialogs = new Mock<IDialogOverlay>();

            var handler = new EzDataRebuildMaintenanceHandler(
                new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.Queued, null, null),
                dialogs.Object,
                notifications: null);

            handler.RequestForceRebuild(new[] { EzDataRebuildAction.RealmChartMsd, EzDataRebuildAction.RealmChartCsi });

            dialogs.Verify(d => d.Push(It.IsAny<PopupDialog>()), Times.Once);
        }

        [Test]
        public void TestForceRebuildWithoutADialogDoesNotDispatch()
        {
            bool dispatched = false;

            var handler = new EzDataRebuildMaintenanceHandler(
                new EzDataRebuildDispatcher((_, _) =>
                {
                    dispatched = true;
                    return EzDataRebuildDispatchResult.Queued;
                }, null, null),
                dialogOverlay: null,
                notifications: null);

            handler.RequestForceRebuild(EzDataRebuildAction.RealmChartMsd);

            Assert.That(dispatched, Is.False, "a destructive rebuild must not run unconfirmed");
        }
    }
}
