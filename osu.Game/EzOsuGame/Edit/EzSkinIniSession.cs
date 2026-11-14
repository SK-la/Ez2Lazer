// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Per-skin <c>skin.ini</c> saved/draft text session. Skin-layer only — not related to <see cref="Configuration.Ez2ConfigManager"/>.
    /// </summary>
    public sealed class EzSkinIniSession
    {
        private readonly SkinManager skinManager;
        private readonly IResourceStore<byte[]> skinFiles;
        private readonly EzSkinIniBackupService backupService;

        public Guid SkinId { get; private set; }

        public string? LastBackupFilename { get; private set; }

        public string SavedText { get; private set; } = string.Empty;

        public string DraftText { get; private set; } = string.Empty;

        public bool IsDirty => IsSupported && DraftText != SavedText;

        public bool IsSupported { get; private set; }

        public event Action? Loaded;

        public EzSkinIniSession(SkinManager skinManager)
        {
            this.skinManager = skinManager;
            skinFiles = ((IStorageResourceProvider)skinManager).Files;
            backupService = new EzSkinIniBackupService(skinManager);
        }

        public void LoadFromSkin(Live<SkinInfo> skinInfo)
        {
            IsSupported = EzSkinIniSupport.IsSupported(skinInfo);
            SkinId = skinInfo.ID;

            if (!IsSupported)
            {
                SavedText = string.Empty;
                DraftText = string.Empty;
                Loaded?.Invoke();
                return;
            }

            string text = skinInfo.PerformRead(readSkinIniText);
            SavedText = text;
            DraftText = text;
            Loaded?.Invoke();
        }

        public void SetDraftText(string text) => DraftText = text;

        public EzSkinIniDocument ParseDraftDocument() => EzSkinIniDocument.Parse(DraftText);

        public void ApplyDocument(EzSkinIniDocument document) => DraftText = document.Serialize();

        public void Discard() => DraftText = SavedText;

        public bool Commit()
        {
            if (!IsSupported || !IsDirty)
                return false;

            var live = skinManager.CurrentSkinInfo.Value;

            if (!EzSkinIniSupport.IsSupported(live))
                return false;

            if (live.ID != SkinId)
                return false;

            string textToCommit = DraftText;
            string savedTextForBackup = SavedText;

            if (live.PerformRead(s => EzLegacySkinFolders.TryGetDirectory(s, out _)))
            {
                live.PerformRead(s =>
                {
                    if (!EzLegacySkinFolders.TryGetDirectory(s, out string directory))
                        return;

                    LastBackupFilename = backupFolderSkinIni(directory, savedTextForBackup);
                    File.WriteAllText(Path.Combine(directory, @"skin.ini"), textToCommit, EzLegacySkinFolders.Utf8NoBom);
                });

                skinManager.ReloadCurrentLegacyFolderSkinIfActive();
            }
            else
            {
                if (!live.IsManaged)
                    return false;

                live.PerformWrite(skin =>
                {
                    LastBackupFilename = backupService.BackupCurrentSkinIni(skin, savedTextForBackup);
                    writeSkinIniText(skin, textToCommit);
                });
                skinManager.CurrentSkinInfo.TriggerChange();
            }

            SavedText = textToCommit;
            DraftText = textToCommit;
            return true;
        }

        private string readSkinIniText(SkinInfo skin)
        {
            if (EzLegacySkinFolders.TryGetDirectory(skin, out string directory))
            {
                string path = Path.Combine(directory, @"skin.ini");

                if (!File.Exists(path))
                    return createDefaultSkinIniText(skin);

                return EzLegacySkinFolders.StripUtf8Bom(File.ReadAllText(path));
            }

            var existingFile = skin.GetFile(@"skin.ini");

            if (existingFile == null)
                return createDefaultSkinIniText(skin);

            using (var stream = skinFiles.GetStream(existingFile.File.GetStoragePath()))
            {
                if (stream == null)
                    return createDefaultSkinIniText(skin);

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private void writeSkinIniText(SkinInfo skin, string text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var existingFile = skin.GetFile(@"skin.ini");

            if (existingFile != null)
                skinManager.ReplaceFile(skin, existingFile, stream);
            else
                skinManager.AddFile(skin, stream, @"skin.ini");
        }

        private static string? backupFolderSkinIni(string directory, string savedText)
        {
            if (string.IsNullOrWhiteSpace(savedText))
                return null;

            string backupFilename = FormattableString.Invariant($"Backup/skin.ini.{DateTime.Now:yyyyMMdd_HHmmss}");
            string backupPath = Path.Combine(directory, backupFilename);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.WriteAllText(backupPath, savedText, EzLegacySkinFolders.Utf8NoBom);
            return backupFilename;
        }

        private static string createDefaultSkinIniText(SkinInfo skin)
        {
            var builder = new StringBuilder();
            builder.AppendLine(@"[General]");
            builder.AppendLine($@"Name: {skin.Name}");
            builder.AppendLine($@"Author: {skin.Creator}");
            builder.AppendLine(FormattableString.Invariant($"Version: {SkinConfiguration.LATEST_VERSION}"));
            return builder.ToString();
        }
    }
}
