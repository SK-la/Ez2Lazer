// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyExporter<ScoreInfo>
    {
        private readonly Func<ScoreInfo, (Score score, IBeatmap beatmap)?>? resolveForEncode;

        public LegacyScoreExporter(Storage storage, Func<ScoreInfo, (Score score, IBeatmap beatmap)?>? resolveForEncode = null)
            : base(storage)
        {
            this.resolveForEncode = resolveForEncode;
        }

        protected override string GetFilename(ScoreInfo score)
        {
            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";

            return filename;
        }

        protected override string FileExtension => @".osr";

        public override void ExportToStream(ScoreInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            var osrFile = model.Files.FirstOrDefault(f => f.Filename.EndsWith(@".osr", StringComparison.OrdinalIgnoreCase));

            if (osrFile != null)
            {
                using (var inputStream = UserFileStorage.GetStream(osrFile.File.GetStoragePath()))
                    inputStream?.CopyTo(outputStream);

                return;
            }

            bool hasEzFrames = model.Files.Any(f => f.Filename.EndsWith(EzHighPrecisionReplayFrames.EXTENSION, StringComparison.OrdinalIgnoreCase));
            if (!hasEzFrames || resolveForEncode == null)
                return;

            var resolved = resolveForEncode(model);
            if (resolved == null)
                return;

            new LegacyScoreEncoder(resolved.Value.score, resolved.Value.beatmap).Encode(outputStream, leaveOpen: true);
        }
    }
}
