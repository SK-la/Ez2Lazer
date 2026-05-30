// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Ez2Lazer client realm: upstream migrations plus <see cref="EZ_REALM_SCHEMA_VERSION"/> Ez migrations.
    /// </summary>
    public sealed class EzRealmSchemaProfile : IRealmSchemaProfile
    {
        /// <summary>必须与 <see cref="RealmAccess"/> 内 <c>schema_version</c>（51）一致。</summary>
        public const int UPSTREAM_SCHEMA_VERSION = 51;

        /// <summary>必须与 <see cref="RealmAccess.EZ_REALM_SCHEMA_VERSION"/> 一致。</summary>
        public const int EZ_REALM_SCHEMA_VERSION = RealmAccess.EZ_REALM_SCHEMA_VERSION;

        public static EzRealmSchemaProfile Instance { get; } = new EzRealmSchemaProfile();

        public int UpstreamSchemaVersion => UPSTREAM_SCHEMA_VERSION;

        public int EzRealmSchemaVersion => EZ_REALM_SCHEMA_VERSION;

        public int FileSchemaVersion => UPSTREAM_SCHEMA_VERSION * 1000 + EZ_REALM_SCHEMA_VERSION;
    }
}
