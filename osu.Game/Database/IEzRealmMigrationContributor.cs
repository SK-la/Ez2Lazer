// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Optional Ez-only Realm migration steps implemented by ruleset assemblies.
    /// Core <see cref="RealmAccess"/> migrations must remain ruleset-agnostic.
    /// </summary>
    public interface IEzRealmMigrationContributor
    {
        /// <summary>
        /// Ez realm versions (1..N) for which <see cref="Apply"/> should run when migrating to that version.
        /// </summary>
        IReadOnlyList<int> TargetEzVersions { get; }

        void Apply(Migration migration, int targetEzVersion);
    }
}
