// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Database;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzDataRebuildDispatcherTest
    {
        [TestCase(EzDataRebuildTarget.RealmTags, EzRealmMetadataScope.Tags)]
        [TestCase(EzDataRebuildTarget.RealmXxy, EzRealmMetadataScope.Xxy)]
        [TestCase(EzDataRebuildTarget.RealmPp, EzRealmMetadataScope.Pp)]
        [TestCase(EzDataRebuildTarget.RealmAll, EzRealmMetadataScope.All)]
        public void TestRealmTargetsDispatchWithCorrectScope(EzDataRebuildTarget target, EzRealmMetadataScope expectedScope)
        {
            EzRealmMetadataScope? capturedScope = null;
            bool? capturedForceAll = null;

            var dispatcher = new EzDataRebuildDispatcher(
                (scope, forceAll) =>
                {
                    capturedScope = scope;
                    capturedForceAll = forceAll;
                    return EzDataRebuildDispatchResult.Queued;
                },
                null,
                null);

            Assert.That(dispatcher.Execute(target, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedScope, Is.EqualTo(expectedScope));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(target, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedScope, Is.EqualTo(expectedScope));
            Assert.That(capturedForceAll, Is.True);
        }

        [Test]
        public void TestSqliteMainDispatchPropagatesForceAll()
        {
            bool? capturedForceAll = null;

            var dispatcher = new EzDataRebuildDispatcher(
                null,
                forceAll =>
                {
                    capturedForceAll = forceAll;
                    return EzDataRebuildDispatchResult.Queued;
                },
                null);

            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteMain, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteMain, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.True);
        }

        [Test]
        public void TestSqliteSongsBranchesDispatchPropagatesForceAll()
        {
            bool? capturedForceAll = null;

            var dispatcher = new EzDataRebuildDispatcher(
                null,
                null,
                forceAll =>
                {
                    capturedForceAll = forceAll;
                    return EzDataRebuildDispatchResult.Queued;
                });

            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteSongsBranches, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteSongsBranches, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.True);
        }

        [TestCase(EzDataRebuildDispatchResult.AlreadyRunning)]
        [TestCase(EzDataRebuildDispatchResult.SqliteDisabled)]
        public void TestQueueDelegateResultIsPropagated(EzDataRebuildDispatchResult delegateResult)
        {
            var dispatcher = new EzDataRebuildDispatcher(
                (_, _) => delegateResult,
                _ => delegateResult,
                _ => delegateResult);

            Assert.That(dispatcher.Execute(EzDataRebuildTarget.RealmAll, forceAll: false), Is.EqualTo(delegateResult));
            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteMain, forceAll: false), Is.EqualTo(delegateResult));
            Assert.That(dispatcher.Execute(EzDataRebuildTarget.SqliteSongsBranches, forceAll: false), Is.EqualTo(delegateResult));
        }

        [TestCase(EzDataRebuildTarget.RealmTags)]
        [TestCase(EzDataRebuildTarget.RealmXxy)]
        [TestCase(EzDataRebuildTarget.RealmPp)]
        [TestCase(EzDataRebuildTarget.RealmAll)]
        public void TestRealmTargetsUnavailableWhenProcessorMissing(EzDataRebuildTarget target)
        {
            var dispatcher = new EzDataRebuildDispatcher((BackgroundDataStoreProcessor?)null, (EzAnalysisWarmupProcessor?)null);

            Assert.That(dispatcher.Execute(target, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnavailableProcessor));
        }

        [TestCase(EzDataRebuildTarget.SqliteMain)]
        [TestCase(EzDataRebuildTarget.SqliteSongsBranches)]
        public void TestSqliteTargetsUnavailableWhenProcessorMissing(EzDataRebuildTarget target)
        {
            var dispatcher = new EzDataRebuildDispatcher((BackgroundDataStoreProcessor?)null, (EzAnalysisWarmupProcessor?)null);

            Assert.That(dispatcher.Execute(target, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnavailableProcessor));
        }

        [Test]
        public void TestUnknownTargetReturnsUnknownTarget()
        {
            var dispatcher = new EzDataRebuildDispatcher(
                (_, _) => EzDataRebuildDispatchResult.Queued,
                _ => EzDataRebuildDispatchResult.Queued,
                _ => EzDataRebuildDispatchResult.Queued);

            Assert.That(dispatcher.Execute((EzDataRebuildTarget)999, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnknownTarget));
        }

        [TestCase(EzDataRebuildTarget.RealmTags, true, false, false)]
        [TestCase(EzDataRebuildTarget.RealmAll, true, false, false)]
        [TestCase(EzDataRebuildTarget.SqliteMain, false, true, false)]
        [TestCase(EzDataRebuildTarget.SqliteSongsBranches, false, false, true)]
        public void TestCanDispatch(EzDataRebuildTarget target, bool hasRealm, bool hasSqliteMain, bool hasSqliteBranches)
        {
            var dispatcher = new EzDataRebuildDispatcher(
                hasRealm ? (_, _) => EzDataRebuildDispatchResult.Queued : null,
                hasSqliteMain ? _ => EzDataRebuildDispatchResult.Queued : null,
                hasSqliteBranches ? _ => EzDataRebuildDispatchResult.Queued : null);

            Assert.That(dispatcher.CanDispatch(target), Is.True);
        }

        [TestCase(EzDataRebuildTarget.RealmTags)]
        [TestCase(EzDataRebuildTarget.RealmAll)]
        public void TestCanDispatchFalseWhenRealmProcessorMissing(EzDataRebuildTarget target)
        {
            var dispatcher = new EzDataRebuildDispatcher(null, _ => EzDataRebuildDispatchResult.Queued, _ => EzDataRebuildDispatchResult.Queued);

            Assert.That(dispatcher.CanDispatch(target), Is.False);
        }

        [TestCase(EzDataRebuildTarget.SqliteMain)]
        [TestCase(EzDataRebuildTarget.SqliteSongsBranches)]
        public void TestCanDispatchFalseWhenWarmupProcessorMissing(EzDataRebuildTarget target)
        {
            var dispatcher = new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.Queued, null, null);

            Assert.That(dispatcher.CanDispatch(target), Is.False);
        }
    }
}
