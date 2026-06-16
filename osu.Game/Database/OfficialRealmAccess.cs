// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;
using osu.Framework.Threading;

namespace osu.Game.Database
{
    /// <summary>
    /// Opens an official osu!lazer <c>client.realm</c> without Ez schema suffixes or Ez migrations.
    /// Use from EzRealmSync and other tools — never use <see cref="RealmAccess"/> for writing official libraries.
    /// </summary>
    public sealed class OfficialRealmAccess : RealmAccess
    {
        public OfficialRealmAccess(Storage storage, string filename, GameThread? updateThread = null, bool allowDestructiveRecoveryOnSchemaMismatch = true)
            : this(storage, filename, updateThread, allowDestructiveRecoveryOnSchemaMismatch, performSchemaMigration: true, pinnedDiskSchemaVersion: null)
        {
        }

        /// <summary>
        /// 以磁盘已有 schema 打开，不执行任何迁移（供 EzRealmSync 等外部工具）。
        /// </summary>
        public new static OfficialRealmAccess OpenWithoutMigration(Storage storage, string filename, int pinnedDiskSchemaVersion, GameThread? updateThread = null)
        {
            return new OfficialRealmAccess(storage, filename, updateThread, allowDestructiveRecoveryOnSchemaMismatch: false, performSchemaMigration: false, pinnedDiskSchemaVersion: (ulong)pinnedDiskSchemaVersion);
        }

        private OfficialRealmAccess(
            Storage storage,
            string filename,
            GameThread? updateThread,
            bool allowDestructiveRecoveryOnSchemaMismatch,
            bool performSchemaMigration,
            ulong? pinnedDiskSchemaVersion)
            : base(storage, filename, RealmSchemaMode.Official, updateThread, useDevelopmentVersionedFilenames: false, allowDestructiveRecoveryOnSchemaMismatch: allowDestructiveRecoveryOnSchemaMismatch, performSchemaMigration: performSchemaMigration, pinnedDiskSchemaVersion: pinnedDiskSchemaVersion)
        {
        }
    }
}
