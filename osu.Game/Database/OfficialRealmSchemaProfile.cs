// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Official osu!lazer client realm: upstream migrations only; disk schema stays at <see cref="UPSTREAM_SCHEMA_VERSION"/>.
    /// </summary>
    public sealed class OfficialRealmSchemaProfile : IRealmSchemaProfile
    {
        public const int UPSTREAM_SCHEMA_VERSION = 51;

        public static OfficialRealmSchemaProfile Instance { get; } = new OfficialRealmSchemaProfile();

        public int UpstreamSchemaVersion => UPSTREAM_SCHEMA_VERSION;

        public int EzRealmSchemaVersion => 0;

        public int FileSchemaVersion => UPSTREAM_SCHEMA_VERSION;
    }
}
