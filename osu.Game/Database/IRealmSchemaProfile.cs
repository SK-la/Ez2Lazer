// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Describes how a <see cref="RealmAccess"/> instance encodes schema version and migrations.
    /// </summary>
    public interface IRealmSchemaProfile
    {
        /// <summary>
        /// Upstream osu!lazer <c>schema_version</c> (currently 51).
        /// </summary>
        int UpstreamSchemaVersion { get; }

        /// <summary>
        /// Ez-only migration revision. Zero for official-compatible databases.
        /// </summary>
        int EzRealmSchemaVersion { get; }

        /// <summary>
        /// Value written to Realm <c>SchemaVersion</c> on disk.
        /// Ez uses <see cref="UpstreamSchemaVersion"/> * 1000 + <see cref="EzRealmSchemaVersion"/>; official uses <see cref="UpstreamSchemaVersion"/> only.
        /// </summary>
        int FileSchemaVersion { get; }
    }
}
