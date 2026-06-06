// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Moq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Overlays;

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
        public void TestCanExecuteRequiresDialogAndDispatchAvailability()
        {
            var dispatcher = new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.Queued, null, null);
            var dialogOverlay = new Mock<IDialogOverlay>().Object;
            var handlerWithDialog = new EzDataRebuildMaintenanceHandler(dispatcher, dialogOverlay, notifications: null);
            var handlerWithoutDialog = new EzDataRebuildMaintenanceHandler(dispatcher, dialogOverlay: null, notifications: null);

            Assert.That(handlerWithDialog.CanExecute(EzDataRebuildTarget.RealmAll), Is.True);
            Assert.That(handlerWithoutDialog.CanExecute(EzDataRebuildTarget.RealmAll), Is.False);
            Assert.That(handlerWithDialog.CanExecute(EzDataRebuildTarget.SqliteMain), Is.False);
        }
    }
}
