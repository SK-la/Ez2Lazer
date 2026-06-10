// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Writes timestamped <c>EzSkin.json</c> backups into the current skin storage before commit.
    /// </summary>
    public sealed class EzSkinJsonBackupService
    {
        private readonly SkinManager skinManager;

        public EzSkinJsonBackupService(SkinManager skinManager)
        {
            this.skinManager = skinManager;
        }

        public string? BackupCurrentSkinJson(SkinInfo skin, string savedJson)
        {
            if (string.IsNullOrWhiteSpace(savedJson))
                return null;

            string backupFilename = FormattableString.Invariant($"Backup/{EzSkinJsonDocument.FILENAME}.{DateTime.Now:yyyyMMdd_HHmmss}");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(savedJson));
            skinManager.AddFile(skin, stream, backupFilename);
            return backupFilename;
        }
    }
}
