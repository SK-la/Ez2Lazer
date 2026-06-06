// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Analysis
{
    public enum EzDataRebuildTarget
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_TAGS))]
        RealmTags = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_XXY))]
        RealmXxy = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_PP))]
        RealmPp = 2,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_ALL))]
        RealmAll = 3,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_SQLITE_MAIN))]
        SqliteMain = 4,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_SQLITE_BRANCHES))]
        SqliteSongsBranches = 5,
    }
}
