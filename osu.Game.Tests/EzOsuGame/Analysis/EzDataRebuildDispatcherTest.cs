// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Database;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzDataRebuildDispatcherTest
    {
        [TestCase(EzDataRebuildAction.RealmTags, EzRealmMetadataScope.Tags)]
        [TestCase(EzDataRebuildAction.RealmSunny, EzRealmMetadataScope.SunnySR)]
        [TestCase(EzDataRebuildAction.RealmPp, EzRealmMetadataScope.PP)]
        [TestCase(EzDataRebuildAction.RealmChartMsd, EzRealmMetadataScope.MSD)]
        [TestCase(EzDataRebuildAction.RealmChartCsi, EzRealmMetadataScope.ChartSkillInfo)]
        [TestCase(EzDataRebuildAction.RealmChartDan, EzRealmMetadataScope.ChartDan)]
        [TestCase(EzDataRebuildAction.RealmPpp, EzRealmMetadataScope.PPP)]
        [TestCase(EzDataRebuildAction.RealmAll, EzRealmMetadataScope.All)]
        public void TestRealmTargetsDispatchWithCorrectScope(EzDataRebuildAction action, EzRealmMetadataScope expectedScope)
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

            Assert.That(dispatcher.Execute(action, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedScope, Is.EqualTo(expectedScope));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(action, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedScope, Is.EqualTo(expectedScope));
            Assert.That(capturedForceAll, Is.True);
        }

        [Test]
        public void TestRealmSelectionIsMergedIntoOneScopeUnion()
        {
            int calls = 0;
            EzRealmMetadataScope capturedScope = default;
            bool? capturedForceAll = null;

            var dispatcher = new EzDataRebuildDispatcher(
                (scope, forceAll) =>
                {
                    calls++;
                    capturedScope = scope;
                    capturedForceAll = forceAll;
                    return EzDataRebuildDispatchResult.Queued;
                },
                null,
                null);

            var actions = new[]
            {
                EzDataRebuildAction.RealmChartMsd,
                EzDataRebuildAction.RealmChartCsi,
                EzDataRebuildAction.RealmChartDan,
            };

            var outcome = dispatcher.Execute(actions, forceAll: true);

            Assert.That(calls, Is.EqualTo(1), "one Realm queue accepts the whole selection");
            Assert.That(capturedScope, Is.EqualTo(EzRealmMetadataScope.MSD | EzRealmMetadataScope.ChartSkillInfo | EzRealmMetadataScope.ChartDan));
            Assert.That(capturedForceAll, Is.True);
            Assert.That(outcome.Queued, Is.EquivalentTo(actions));
            Assert.That(outcome.Failures, Is.Empty);
        }

        [Test]
        public void TestEachQueueIsDispatchedOnceForAMixedSelection()
        {
            int realmCalls = 0;
            int scoreCalls = 0;
            int sqliteMainCalls = 0;

            var dispatcher = new EzDataRebuildDispatcher(
                (_, _) =>
                {
                    realmCalls++;
                    return EzDataRebuildDispatchResult.Queued;
                },
                _ =>
                {
                    sqliteMainCalls++;
                    return EzDataRebuildDispatchResult.Queued;
                },
                null,
                _ =>
                {
                    scoreCalls++;
                    return EzDataRebuildDispatchResult.Queued;
                });

            var actions = new[]
            {
                EzDataRebuildAction.RealmTags,
                EzDataRebuildAction.RealmScores,
                EzDataRebuildAction.SqliteMain,
            };

            var outcome = dispatcher.Execute(actions, forceAll: false);

            Assert.That(new[] { realmCalls, scoreCalls, sqliteMainCalls }, Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(outcome.Queued, Is.EquivalentTo(actions));
        }

        [Test]
        public void TestMergedGroupPropagatesTheQueueResultToEveryAction()
        {
            var dispatcher = new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.AlreadyRunning, null, null);

            var actions = new[] { EzDataRebuildAction.RealmChartMsd, EzDataRebuildAction.RealmChartCsi };
            var outcome = dispatcher.Execute(actions, forceAll: false);

            Assert.That(outcome.Queued, Is.Empty);
            Assert.That(outcome.Failures.Select(f => f.Action), Is.EquivalentTo(actions));
            Assert.That(outcome.FirstFailure, Is.EqualTo(EzDataRebuildDispatchResult.AlreadyRunning));
        }

        [Test]
        public void TestRealmScoresDispatchPropagatesForceAll()
        {
            bool? capturedForceAll = null;

            var dispatcher = new EzDataRebuildDispatcher(
                null,
                null,
                null,
                forceAll =>
                {
                    capturedForceAll = forceAll;
                    return EzDataRebuildDispatchResult.Queued;
                });

            Assert.That(dispatcher.Execute(EzDataRebuildAction.RealmScores, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(EzDataRebuildAction.RealmScores, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.True);
        }

        [Test]
        public void TestRealmScoresUnavailableWhenProcessorMissing()
        {
            var dispatcher = new EzDataRebuildDispatcher(null, null);

            Assert.That(dispatcher.CanDispatch(EzDataRebuildAction.RealmScores), Is.False);
            Assert.That(dispatcher.Execute(EzDataRebuildAction.RealmScores, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnavailableProcessor));
        }

        [Test]
        public void TestRealmScoresCanDispatch()
        {
            var dispatcher = new EzDataRebuildDispatcher(null, null, null, _ => EzDataRebuildDispatchResult.Queued);

            Assert.That(dispatcher.CanDispatch(EzDataRebuildAction.RealmScores), Is.True);
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

            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteMain, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteMain, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
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

            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteSongsBranches, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
            Assert.That(capturedForceAll, Is.False);

            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteSongsBranches, forceAll: true), Is.EqualTo(EzDataRebuildDispatchResult.Queued));
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

            Assert.That(dispatcher.Execute(EzDataRebuildAction.RealmAll, forceAll: false), Is.EqualTo(delegateResult));
            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteMain, forceAll: false), Is.EqualTo(delegateResult));
            Assert.That(dispatcher.Execute(EzDataRebuildAction.SqliteSongsBranches, forceAll: false), Is.EqualTo(delegateResult));
        }

        [TestCase(EzDataRebuildAction.RealmTags)]
        [TestCase(EzDataRebuildAction.RealmSunny)]
        [TestCase(EzDataRebuildAction.RealmPp)]
        [TestCase(EzDataRebuildAction.RealmChartMsd)]
        [TestCase(EzDataRebuildAction.RealmAll)]
        public void TestRealmTargetsUnavailableWhenProcessorMissing(EzDataRebuildAction action)
        {
            var dispatcher = new EzDataRebuildDispatcher(null, null);

            Assert.That(dispatcher.Execute(action, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnavailableProcessor));
        }

        [TestCase(EzDataRebuildAction.SqliteMain)]
        [TestCase(EzDataRebuildAction.SqliteSongsBranches)]
        public void TestSqliteTargetsUnavailableWhenProcessorMissing(EzDataRebuildAction action)
        {
            var dispatcher = new EzDataRebuildDispatcher(null, null);

            Assert.That(dispatcher.Execute(action, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnavailableProcessor));
        }

        [Test]
        public void TestUnknownTargetReturnsUnknownTarget()
        {
            var dispatcher = new EzDataRebuildDispatcher(
                (_, _) => EzDataRebuildDispatchResult.Queued,
                _ => EzDataRebuildDispatchResult.Queued,
                _ => EzDataRebuildDispatchResult.Queued);

            Assert.That(dispatcher.Execute((EzDataRebuildAction)999, forceAll: false), Is.EqualTo(EzDataRebuildDispatchResult.UnknownTarget));
        }

        [TestCase(EzDataRebuildAction.RealmTags, true, false, false)]
        [TestCase(EzDataRebuildAction.RealmChartMsd, true, false, false)]
        [TestCase(EzDataRebuildAction.RealmAll, true, false, false)]
        [TestCase(EzDataRebuildAction.SqliteMain, false, true, false)]
        [TestCase(EzDataRebuildAction.SqliteSongsBranches, false, false, true)]
        public void TestCanDispatch(EzDataRebuildAction action, bool hasRealm, bool hasSqliteMain, bool hasSqliteBranches)
        {
            var dispatcher = new EzDataRebuildDispatcher(
                hasRealm ? (_, _) => EzDataRebuildDispatchResult.Queued : null,
                hasSqliteMain ? _ => EzDataRebuildDispatchResult.Queued : null,
                hasSqliteBranches ? _ => EzDataRebuildDispatchResult.Queued : null);

            Assert.That(dispatcher.CanDispatch(action), Is.True);
        }

        [TestCase(EzDataRebuildAction.RealmTags)]
        [TestCase(EzDataRebuildAction.RealmChartMsd)]
        [TestCase(EzDataRebuildAction.RealmAll)]
        public void TestCanDispatchFalseWhenRealmProcessorMissing(EzDataRebuildAction action)
        {
            var dispatcher = new EzDataRebuildDispatcher(null, _ => EzDataRebuildDispatchResult.Queued, _ => EzDataRebuildDispatchResult.Queued);

            Assert.That(dispatcher.CanDispatch(action), Is.False);
        }

        [TestCase(EzDataRebuildAction.SqliteMain)]
        [TestCase(EzDataRebuildAction.SqliteSongsBranches)]
        public void TestCanDispatchFalseWhenWarmupProcessorMissing(EzDataRebuildAction action)
        {
            var dispatcher = new EzDataRebuildDispatcher((_, _) => EzDataRebuildDispatchResult.Queued, null, null);

            Assert.That(dispatcher.CanDispatch(action), Is.False);
        }
    }
}
