// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 外部传统皮肤的目录约定：<c>EzResources/LegacySkin/&lt;文件夹&gt;</c>。
    /// 文件夹名存在 <see cref="SkinInfo.Hash"/>，稳定 ID 存在 <c>skininfo.json</c>。上游没有对应存储，所以只放在这里。
    /// </summary>
    public static class EzLegacySkinFolders
    {
        private const string meta_filename = "skininfo.json";
        private const string migration_marker = ".migrated";

        private static readonly string legacy_instantiation = typeof(LegacySkin).GetInvariantInstantiationInfo();

        private static string? root;

        public static void BindRoot(Storage storage)
        {
            root = storage.GetFullPath(EzModifyPath.LEGACY_SKIN_PATH);
            Directory.CreateDirectory(root);
        }

        public static bool TryGetDirectory(SkinInfo skin, out string directory)
        {
            directory = string.Empty;

            if (root == null || string.IsNullOrEmpty(skin.Hash) || skin.InstantiationInfo != legacy_instantiation)
                return false;

            string candidate = Path.Combine(root, skin.Hash);

            if (!Directory.Exists(candidate))
                return false;

            directory = candidate;
            return true;
        }

        public static bool AnyFolders()
        {
            if (root == null || !Directory.Exists(root))
                return false;

            return Directory.EnumerateDirectories(root).Any();
        }

        public static List<SkinInfo> Scan()
        {
            var skins = new List<SkinInfo>();

            if (root == null || !Directory.Exists(root))
                return skins;

            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                string folderName = Path.GetFileName(directory);

                if (string.IsNullOrEmpty(folderName))
                    continue;

                skins.Add(createInfo(folderName, directory));
            }

            return skins;
        }

        public static IEnumerable<string> ImageFileNames(string directory)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');

                if (SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(relative).ToLowerInvariant()))
                    yield return relative;
            }
        }

        public static SkinInfo ImportArchive(Storage storage, ArchiveReader archive, IEnumerable<(string original, string shortened)> files)
        {
            BindRoot(storage);

            string folderName = nextFolderName(sanitize(Path.GetFileNameWithoutExtension(archive.Name ?? "skin")));
            string directory = Path.Combine(root!, folderName);
            Directory.CreateDirectory(directory);

            try
            {
                foreach ((string original, string shortened) in files)
                {
                    if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(shortened))
                        throw new InvalidOperationException($@"Filename ""{original}"" is not allowed.");

                    string destination = Path.Combine(directory, shortened.Replace('/', Path.DirectorySeparatorChar));
                    string? parent = Path.GetDirectoryName(destination);

                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    using (Stream input = archive.GetStream(original))
                    using (FileStream output = File.Create(destination))
                        input.CopyTo(output);
                }
            }
            catch
            {
                Directory.Delete(directory, true);
                throw;
            }

            if (!File.Exists(Path.Combine(directory, meta_filename)))
                writeMeta(directory, new FolderMeta { ID = Guid.NewGuid() });

            return createInfo(folderName, directory);
        }

        public static bool Save(Skin skin, string directory)
        {
            bool changed = false;
            SkinInfo info = skin.SkinInfo.Value;
            FolderMeta meta = readMeta(directory) ?? new FolderMeta { ID = info.ID };
            meta.ID = info.ID == Guid.Empty ? meta.ID : info.ID;
            meta.Name = info.Name;
            meta.Creator = info.Creator;
            changed |= writeMeta(directory, meta);

            foreach (var layout in skin.LayoutInfos)
                changed |= writeIfDifferent(Path.Combine(directory, $"{layout.Key}.json"), JsonConvert.SerializeObject(layout.Value, Formatting.Indented));

            return changed;
        }

        public static void WriteSkinIni(Skin skin, string directory)
        {
            string path = Path.Combine(directory, "skin.ini");

            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
                new LegacySkinEncoder(skin).Encode(writer);

            writeIfDifferent(path, Encoding.UTF8.GetString(stream.ToArray()));
        }

        public static string? Rename(SkinInfo info, string newName, IStorageResourceProvider resources)
        {
            if (!TryGetDirectory(info, out string directory))
                return null;

            string updatedName = string.IsNullOrWhiteSpace(newName) ? info.Name : newName.Trim();
            var skin = new LegacySkin(info, resources, new StorageBackedResourceStore(new NativeStorage(directory)));

            try
            {
                skin.Configuration.SkinInfo.Name = updatedName;
                skin.Configuration.SkinInfo.Creator = info.Creator;
                WriteSkinIni(skin, directory);
            }
            finally
            {
                skin.Dispose();
            }

            FolderMeta meta = readMeta(directory) ?? new FolderMeta { ID = info.ID };
            meta.ID = info.ID;
            meta.Name = updatedName;
            meta.Creator = info.Creator;
            writeMeta(directory, meta);

            string renamed = nextFolderName(sanitize(updatedName), info.Hash);

            if (!string.Equals(renamed, info.Hash, StringComparison.OrdinalIgnoreCase))
                Directory.Move(directory, Path.Combine(root!, renamed));

            return renamed;
        }

        public static void Delete(SkinInfo info)
        {
            if (!TryGetDirectory(info, out string directory))
                return;

            string temporary = directory + ".deleting";

            if (Directory.Exists(temporary))
                Directory.Delete(temporary, true);

            Directory.Move(directory, temporary);
            Directory.Delete(temporary, true);
        }

        /// <summary>
        /// 把 Realm 里的传统皮肤复制到目录。全部成功才写标记；失败的行保持原样，下次启动重试。
        /// </summary>
        public static void Migrate(RealmAccess realm, Storage storage)
        {
            BindRoot(storage);

            string marker = Path.Combine(root!, migration_marker);

            if (File.Exists(marker))
                return;

            string filesRoot = storage.GetFullPath("files");
            List<MigrationSource> sources = realm.Run(collectSources);
            var failed = new HashSet<Guid>();

            foreach (MigrationSource source in sources)
            {
                try
                {
                    migrateOne(source, filesRoot);
                }
                catch (Exception e)
                {
                    failed.Add(source.ID);
                    Logger.Error(e, $"未能把传统皮肤「{source.Name}」迁到 {EzModifyPath.LEGACY_SKIN_PATH}。");
                }
            }

            realm.Write(r =>
            {
                foreach (MigrationSource source in sources)
                {
                    if (failed.Contains(source.ID))
                        continue;

                    SkinInfo? row = r.Find<SkinInfo>(source.ID);

                    row?.DeletePending = true;
                }
            });

            if (failed.Count == 0)
                File.WriteAllText(marker, "1");
        }

        private static List<MigrationSource> collectSources(Realms.Realm realm)
        {
            var sources = new List<MigrationSource>();

            foreach (SkinInfo skin in realm.All<SkinInfo>().Where(s => !s.Protected && !s.DeletePending))
            {
                if (!isMigratable(skin))
                    continue;

                var files = new List<(string Filename, string StoragePath)>();

                if (skin.Files.Count != 0)
                {
                    foreach (var file in skin.Files)
                    {
                        files.Add((file.Filename, file.File.GetStoragePath()));
                    }
                }

                sources.Add(new MigrationSource(skin.ID, skin.Name, skin.Creator, files));
            }

            return sources;
        }

        private static void migrateOne(MigrationSource source, string filesRoot)
        {
            if (findDirectoryById(source.ID) != null)
                return;

            string folderName = nextFolderName(sanitize(string.IsNullOrWhiteSpace(source.Name) ? "skin" : source.Name));
            string directory = Path.Combine(root!, folderName);
            Directory.CreateDirectory(directory);

            try
            {
                foreach ((string filename, string storagePath) in source.Files)
                {
                    if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename))
                        throw new InvalidOperationException($@"Filename ""{filename}"" is not allowed.");

                    string sourcePath = Path.Combine(filesRoot, storagePath);
                    string destination = Path.Combine(directory, filename.Replace('/', Path.DirectorySeparatorChar));
                    string? parent = Path.GetDirectoryName(destination);

                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    File.Copy(sourcePath, destination, true);
                }

                writeMeta(directory, new FolderMeta
                {
                    ID = source.ID,
                    Name = source.Name,
                    Creator = source.Creator,
                });
            }
            catch
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);

                throw;
            }
        }

        private static bool isMigratable(SkinInfo skin) =>
            string.IsNullOrEmpty(skin.InstantiationInfo) || skin.InstantiationInfo == legacy_instantiation;

        private static SkinInfo createInfo(string folderName, string directory)
        {
            (string iniName, string iniCreator) = readIni(directory);
            FolderMeta? meta = readMeta(directory);

            return new SkinInfo
            {
                ID = meta?.ID is Guid id && id != Guid.Empty ? id : idForFolder(folderName),
                Name = firstNonEmpty(iniName, meta?.Name, folderName),
                Creator = firstNonEmpty(iniCreator, meta?.Creator),
                Hash = folderName,
                Protected = false,
                InstantiationInfo = legacy_instantiation,
            };
        }

        private static (string name, string creator) readIni(string directory)
        {
            string path = Path.Combine(directory, "skin.ini");

            if (!File.Exists(path))
                return (string.Empty, string.Empty);

            try
            {
                using var stream = File.OpenRead(path);
                var configuration = new LegacySkinDecoder().Decode(new LineBufferedReader(stream));
                return (configuration.SkinInfo.Name, configuration.SkinInfo.Creator);
            }
            catch (Exception e)
            {
                Logger.Log($"无法读取 {path}：{e.Message}", LoggingTarget.Database, LogLevel.Error);
                return (string.Empty, string.Empty);
            }
        }

        private static string? findDirectoryById(Guid id)
        {
            if (root == null || !Directory.Exists(root))
                return null;

            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                if (readMeta(directory)?.ID == id)
                    return directory;
            }

            return null;
        }

        private static string nextFolderName(string desired, string? keep = null)
        {
            if (isFree(desired, keep))
                return desired;

            for (int i = 2; ; i++)
            {
                string candidate = $"{desired} ({i})";

                if (isFree(candidate, keep))
                    return candidate;
            }
        }

        private static bool isFree(string folderName, string? keep) =>
            string.Equals(folderName, keep, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(Path.Combine(root!, folderName));

        private static string sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            name = name.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(name) ? "skin" : name;
        }

        private static Guid idForFolder(string folderName)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes("legacyfolder:" + folderName));
            return new Guid(hash.AsSpan(0, 16));
        }

        private static string firstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static FolderMeta? readMeta(string directory)
        {
            string path = Path.Combine(directory, meta_filename);

            if (!File.Exists(path))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<FolderMeta>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Logger.Log($"无法读取 {path}：{e.Message}", LoggingTarget.Database, LogLevel.Error);
                return null;
            }
        }

        private static bool writeMeta(string directory, FolderMeta meta) =>
            writeIfDifferent(Path.Combine(directory, meta_filename), JsonConvert.SerializeObject(meta, Formatting.Indented));

        private static bool writeIfDifferent(string path, string contents)
        {
            if (File.Exists(path) && File.ReadAllText(path) == contents)
                return false;

            File.WriteAllText(path, contents, Encoding.UTF8);
            return true;
        }

        private sealed class FolderMeta
        {
            public Guid ID { get; set; }

            public string Name { get; set; } = string.Empty;

            public string Creator { get; set; } = string.Empty;
        }

        private sealed class MigrationSource
        {
            public MigrationSource(Guid id, string name, string creator, List<(string Filename, string StoragePath)> files)
            {
                ID = id;
                Name = name;
                Creator = creator;
                Files = files;
            }

            public Guid ID { get; }

            public string Name { get; }

            public string Creator { get; }

            public List<(string Filename, string StoragePath)> Files { get; }
        }
    }
}
