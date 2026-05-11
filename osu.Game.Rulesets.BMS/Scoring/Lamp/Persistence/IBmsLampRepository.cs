// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence
{
    /// <summary>
    /// Storage adapter contract for the BMS lamp record set. Exists so the in-memory
    /// <see cref="BmsLampStore"/> stays oblivious to whether records live in SQLite, JSON,
    /// a remote service, or only in-process tests. All methods are expected to be safe to
    /// call from the gameplay/UI threads — implementations push slow I/O to a worker as needed.
    /// </summary>
    public interface IBmsLampRepository : IDisposable
    {
        /// <summary>
        /// Eagerly load every persisted record. Called once at attach time; the result is
        /// folded into the in-memory dictionary and never re-queried during normal operation.
        /// </summary>
        /// <remarks>
        /// Failures must not throw — implementations should log + return an empty collection
        /// so a corrupted store never blocks song-select from opening.
        /// </remarks>
        IReadOnlyCollection<BmsLampRecord> LoadAll();

        /// <summary>
        /// Persist a single record, replacing any previous row for the same beatmap.
        /// </summary>
        /// <remarks>
        /// "Best lamp" arbitration is the caller's job — by the time a record reaches this
        /// method it is already the canonical row that should be written. This keeps the
        /// repository deterministic and easy to test (write-what-you-get).
        /// </remarks>
        void Upsert(BmsLampRecord record);

        /// <summary>
        /// Optional delete; used by the future "clear lamp history" admin action. Implementations
        /// that don't support it can no-op.
        /// </summary>
        void Delete(Guid beatmapId);
    }
}
