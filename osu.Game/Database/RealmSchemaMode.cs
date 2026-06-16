// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Selects how <see cref="RealmAccess"/> encodes disk schema version and which migrations run.
    /// </summary>
    public enum RealmSchemaMode
    {
        /// <summary>
        /// Ez2Lazer client: upstream migrations plus <see cref="RealmAccess.EZ_REALM_SCHEMA_VERSION"/> Ez migrations.
        /// Disk schema is <see cref="RealmAccess.EzFileSchemaVersion"/>.
        /// </summary>
        Ez,

        /// <summary>
        /// Official osu!lazer client realm: upstream migrations only.
        /// Disk schema is <see cref="RealmAccess.UpstreamSchemaVersion"/>.
        /// </summary>
        Official,
    }
}
