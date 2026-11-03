// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Writes timestamped <c>skin.ini</c> backups into the current skin storage before commit.
    /// </summary>
    public sealed class EzSkinIniBackupService
    {
        private readonly SkinManager skinManager;

        public EzSkinIniBackupService(SkinManager skinManager)
        {
            this.skinManager = skinManager;
        }

        public string? BackupCurrentSkinIni(SkinInfo skin, string savedText)
        {
            if (string.IsNullOrWhiteSpace(savedText))
                return null;

            string backupFilename = FormattableString.Invariant($"Backup/skin.ini.{DateTime.Now:yyyyMMdd_HHmmss}");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(savedText));
            skinManager.AddFile(skin, stream, backupFilename);
            return backupFilename;
        }
    }
}
