// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    /// <summary>
    /// A scoped skills pass is narrowed to the players known to be behind. Deriving that narrowing by intersecting the
    /// scope with the selection is what left flagged players nobody could ever clear, and dropping a scoped name the
    /// archive no longer covers would leave rows reporting work no pass can do.
    /// </summary>
    [TestFixture]
    public class EzPlayerSkillRefreshScopeTest
    {
        private static readonly string[] selected = { "alpha", "beta" };
        private static readonly string[] included = { "alpha", "beta", "gamma" };

        [Test]
        public void No_scope_refreshes_every_selected_player()
        {
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, null, included);

            Assert.That(refresh, Is.EquivalentTo(selected));
            Assert.That(orphaned, Is.Empty);
        }

        [Test]
        public void A_scoped_player_outside_the_selection_is_still_refreshed()
        {
            // The ingest path flags a player without asking which ones the dialog ticked, so the scope has to be able
            // to reach a player this run is not otherwise re-aggregating.
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, new[] { "gamma" }, included);

            Assert.That(refresh, Is.EquivalentTo(new[] { "gamma" }));
            Assert.That(orphaned, Is.Empty);
        }

        [Test]
        public void A_selected_player_outside_the_scope_is_left_alone()
        {
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, new[] { "beta" }, included);

            Assert.That(refresh, Is.EquivalentTo(new[] { "beta" }));
            Assert.That(orphaned, Is.Empty);
        }

        [Test]
        public void A_scoped_player_the_archive_does_not_cover_is_dropped_rather_than_refreshed()
        {
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, new[] { "alpha", "stranger" }, included);

            Assert.That(refresh, Is.EquivalentTo(new[] { "alpha" }));
            Assert.That(orphaned, Is.EquivalentTo(new[] { "stranger" }));
        }

        [Test]
        public void Names_are_not_refreshed_twice()
        {
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, new[] { "alpha", "alpha", "beta" }, included);

            Assert.That(refresh, Is.EquivalentTo(selected));
            Assert.That(orphaned, Is.Empty);
        }

        [Test]
        public void An_empty_scope_refreshes_nobody()
        {
            var (refresh, orphaned) = EzPlayerSkillRefreshScope.Resolve(selected, Array.Empty<string>(), included);

            Assert.That(refresh, Is.Empty);
            Assert.That(orphaned, Is.Empty);
        }
    }
}
