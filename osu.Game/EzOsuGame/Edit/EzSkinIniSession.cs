// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Extensions;
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

        public bool IsDirty => DraftText != SavedText;

        public EzSkinIniSession(SkinManager skinManager)
        {
            this.skinManager = skinManager;
            skinFiles = ((IStorageResourceProvider)skinManager).Files;
            backupService = new EzSkinIniBackupService(skinManager);
        }

        public void LoadFromSkin(Live<SkinInfo> skinInfo)
        {
            string text = skinInfo.PerformRead(readSkinIniText);
            SkinId = skinInfo.ID;
            SavedText = text;
            DraftText = text;
        }

        public void SetDraftText(string text) => DraftText = text;

        public EzSkinIniDocument ParseDraftDocument() => EzSkinIniDocument.Parse(DraftText);

        public void ApplyDocument(EzSkinIniDocument document) => DraftText = document.Serialize();

        public void Discard() => DraftText = SavedText;

        public bool Commit()
        {
            if (!IsDirty)
                return false;

            var live = skinManager.CurrentSkinInfo.Value;

            if (live.ID != SkinId)
                throw new InvalidOperationException("Cannot commit skin.ini for a skin that is no longer current.");

            live.PerformWrite(skin =>
            {
                LastBackupFilename = backupService.BackupCurrentSkinIni(skin, SavedText);
                writeSkinIniText(skin, DraftText);
            });
            skinManager.CurrentSkinInfo.TriggerChange();
            SavedText = DraftText;
            return true;
        }

        private string readSkinIniText(SkinInfo skin)
        {
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
