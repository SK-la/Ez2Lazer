// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    /// <summary>
    /// SQLite layouts aligned with beatoraja song / score / scorelog / information tables
    /// so <c>folder/default.json</c> WHERE clauses resolve without missing columns.
    /// </summary>
    internal static class BmsFilterSchema
    {
        public const string CREATE_SONG = @"
CREATE TABLE song (
    md5 TEXT,
    sha256 TEXT PRIMARY KEY,
    title TEXT,
    subtitle TEXT,
    genre TEXT,
    artist TEXT,
    subartist TEXT,
    path TEXT,
    folder TEXT,
    parent TEXT,
    level INTEGER,
    difficulty INTEGER,
    mode INTEGER,
    notes INTEGER,
    favorite INTEGER,
    maxbpm INTEGER,
    minbpm INTEGER,
    length INTEGER,
    date INTEGER,
    adddate INTEGER
);";

        public const string CREATE_SCORE = @"
CREATE TABLE score (
    sha256 TEXT PRIMARY KEY,
    mode INTEGER,
    clear INTEGER,
    playcount INTEGER,
    clearcount INTEGER,
    epg INTEGER,
    lpg INTEGER,
    egr INTEGER,
    lgr INTEGER,
    egd INTEGER,
    lgd INTEGER,
    ebd INTEGER,
    lbd INTEGER,
    epr INTEGER,
    lpr INTEGER,
    ems INTEGER,
    lms INTEGER,
    notes INTEGER,
    combo INTEGER,
    minbp INTEGER,
    avgjudge INTEGER,
    date INTEGER
);";

        public const string CREATE_SCORELOG = @"
CREATE TABLE scorelog (
    sha256 TEXT,
    mode INTEGER,
    clear INTEGER,
    playcount INTEGER,
    clearcount INTEGER,
    epg INTEGER,
    lpg INTEGER,
    egr INTEGER,
    lgr INTEGER,
    notes INTEGER,
    combo INTEGER,
    minbp INTEGER,
    date INTEGER
);";

        public const string CREATE_INFORMATION = @"
CREATE TABLE information (
    sha256 TEXT PRIMARY KEY,
    n INTEGER,
    ln INTEGER,
    s INTEGER,
    ls INTEGER,
    total REAL,
    density REAL,
    peakdensity REAL,
    enddensity REAL,
    mainbpm REAL
);";

        /// <summary>
        /// Mirrors beatoraja <see cref="SQLiteSongDatabaseAccessor.getSongDatas"/> join shape.
        /// Note: This query template intentionally ends with "WHERE " to allow dynamic condition appending.
        /// </summary>
        public const string SELECT_DISTINCT_SHA256 = @"
SELECT DISTINCT song.sha256
FROM song
LEFT OUTER JOIN information ON song.sha256 = information.sha256
LEFT OUTER JOIN score ON song.sha256 = score.sha256
LEFT OUTER JOIN scorelog ON score.sha256 = scorelog.sha256
WHERE ";
    }
}
