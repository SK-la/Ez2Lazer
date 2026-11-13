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

        public static string? RootDirectory { get; private set; }

        public static bool IsMigrationFinished
        {
            get
            {
                try
                {
                    return RootDirectory != null && File.Exists(Path.Combine(RootDirectory, migration_marker));
                }
                catch (Exception e)
                {
                    Logger.Error(e, "无法确认传统皮肤是否已经迁移。");
                    return false;
                }
            }
        }

        public static void BindRoot(Storage storage)
        {
            try
            {
                RootDirectory = storage.GetFullPath(EzModifyPath.LEGACY_SKIN_PATH);
                Directory.CreateDirectory(RootDirectory);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"无法创建 {EzModifyPath.LEGACY_SKIN_PATH}。");
            }
        }

        public static bool TryGetDirectory(SkinInfo skin, out string directory)
        {
            directory = string.Empty;

            try
            {
                if (RootDirectory == null || string.IsNullOrEmpty(skin.Hash) || skin.InstantiationInfo != legacy_instantiation)
                    return false;

                string candidate = Path.Combine(RootDirectory, skin.Hash);

                if (!Directory.Exists(candidate))
                    return false;

                directory = candidate;
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "读取传统皮肤目录失败。");
                return false;
            }
        }

        public static bool AnyFolders()
        {
            try
            {
                if (RootDirectory == null || !Directory.Exists(RootDirectory))
                    return false;

                return Directory.EnumerateDirectories(RootDirectory).Any();
            }
            catch (Exception e)
            {
                Logger.Error(e, "无法枚举传统皮肤目录。");
                return false;
            }
        }

        public static List<SkinInfo> Scan()
        {
            var skins = new List<SkinInfo>();

            if (RootDirectory == null)
                return skins;

            IEnumerable<string> directories;

            try
            {
                if (!Directory.Exists(RootDirectory))
                    return skins;

                directories = Directory.EnumerateDirectories(RootDirectory);
            }
            catch (Exception e)
            {
                Logger.Error(e, "无法枚举传统皮肤目录。");
                return skins;
            }

            foreach (string directory in directories)
            {
                string folderName = Path.GetFileName(directory);

                if (string.IsNullOrEmpty(folderName) || folderName[0] == '.' || folderName.EndsWith(".deleting", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    skins.Add(createInfo(folderName, directory));
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"跳过无法读取的传统皮肤文件夹「{folderName}」。");
                }
            }

            return skins;
        }

        public static IEnumerable<string> ImageFileNames(string directory)
        {
            var names = new List<string>();

            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');

                    if (SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(relative).ToLowerInvariant()))
                        names.Add(relative);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"无法列出传统皮肤图片：{directory}");
            }

            return names;
        }

        public static SkinInfo ImportArchive(Storage storage, ArchiveReader archive, IEnumerable<(string original, string shortened)> files)
        {
            BindRoot(storage);

            if (RootDirectory == null)
                throw new IOException($"无法创建 {EzModifyPath.LEGACY_SKIN_PATH}。");

            string folderName = nextFolderName(sanitize(Path.GetFileNameWithoutExtension(archive.Name ?? "skin")));
            string directory = Path.Combine(RootDirectory, folderName);
            Directory.CreateDirectory(directory);

            try
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var renames = new List<(string from, string to)>();

                foreach ((string original, string shortened) in files)
                {
                    if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(shortened))
                        throw new InvalidOperationException($@"Filename ""{original}"" is not allowed.");

                    tryWriteRelativeFile(directory, folderName, shortened, used, renames, destination =>
                    {
                        using Stream input = archive.GetStream(original);
                        using FileStream output = File.Create(destination);
                        input.CopyTo(output);
                    });
                }

                rewriteRenamedReferences(directory, renames);
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
            {
                try
                {
                    Directory.Move(directory, Path.Combine(RootDirectory!, renamed));
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"无法重命名传统皮肤文件夹「{info.Hash}」。");
                    return info.Hash;
                }
            }

            return renamed;
        }

        public static void Delete(SkinInfo info)
        {
            try
            {
                if (!TryGetDirectory(info, out string directory))
                    return;

                string temporary = directory + ".deleting";

                if (Directory.Exists(temporary))
                    Directory.Delete(temporary, true);

                Directory.Move(directory, temporary);
                Directory.Delete(temporary, true);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"无法删除传统皮肤「{info.Name}」。");
            }
        }

        /// <summary>
        /// 复制传统皮肤到目录。<paramref name="report"/> 在调用线程上回调，参数是已处理数、总数、当前名称。
        /// 返回 false 表示被取消或有失败，下次启动会重试。
        /// </summary>
        public static bool Migrate(RealmAccess realm, Storage storage, Action<int, int, string>? report = null, Func<bool>? cancelled = null)
        {
            try
            {
                BindRoot(storage);

                if (RootDirectory == null)
                    return false;

                if (IsMigrationFinished)
                    return true;

                string filesRoot = storage.GetFullPath("files");
                List<MigrationSource> sources = realm.Run(collectSources);
                var succeeded = new List<Guid>();
                int failed = 0;
                bool stopped = false;

                for (int i = 0; i < sources.Count; i++)
                {
                    if (cancelled?.Invoke() == true)
                    {
                        stopped = true;
                        break;
                    }

                    MigrationSource source = sources[i];
                    report?.Invoke(i, sources.Count, source.Name);

                    try
                    {
                        migrateOne(source, filesRoot);
                        succeeded.Add(source.ID);
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Logger.Error(e, $"未能把传统皮肤「{source.Name}」迁到 {EzModifyPath.LEGACY_SKIN_PATH}。");
                    }
                }

                report?.Invoke(sources.Count, sources.Count, string.Empty);
                markMigrated(realm, succeeded, writeMarker: !stopped && failed == 0);
                return !stopped && failed == 0;
            }
            catch (Exception e)
            {
                Logger.Error(e, "传统皮肤迁移中断。下次启动会重试。");
                return false;
            }
        }

        private static void markMigrated(RealmAccess realm, List<Guid> succeeded, bool writeMarker)
        {
            if (succeeded.Count > 0)
            {
                realm.Write(r =>
                {
                    foreach (Guid id in succeeded)
                    {
                        SkinInfo? row = r.Find<SkinInfo>(id);

                        if (row != null)
                            row.DeletePending = true;
                    }
                });
            }

            if (!writeMarker || RootDirectory == null)
                return;

            File.WriteAllText(Path.Combine(RootDirectory, migration_marker), "1");
        }

        private static List<MigrationSource> collectSources(Realms.Realm realm)
        {
            var sources = new List<MigrationSource>();

            foreach (SkinInfo skin in realm.All<SkinInfo>().Where(s => !s.Protected && !s.DeletePending))
            {
                if (!isMigratable(skin))
                    continue;

                try
                {
                    var files = new List<(string Filename, string StoragePath)>();

                    foreach (var file in skin.Files)
                    {
                        if (string.IsNullOrEmpty(file.Filename))
                            continue;

                        files.Add((file.Filename, file.File.GetStoragePath()));
                    }

                    sources.Add(new MigrationSource(skin.ID, skin.Name, skin.Creator, files));
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"跳过无法读取的传统皮肤「{skin.Name}」。");
                }
            }

            return sources;
        }

        private static void migrateOne(MigrationSource source, string filesRoot)
        {
            if (findDirectoryById(source.ID) != null)
                return;

            string folderName = nextFolderName(sanitize(string.IsNullOrWhiteSpace(source.Name) ? "skin" : source.Name));
            string directory = Path.Combine(RootDirectory!, folderName);
            Directory.CreateDirectory(directory);

            try
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var renames = new List<(string from, string to)>();

                foreach ((string filename, string storagePath) in source.Files)
                {
                    if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename))
                        throw new InvalidOperationException($@"Filename ""{filename}"" is not allowed.");

                    string sourcePath = Path.Combine(filesRoot, storagePath);
                    tryWriteRelativeFile(directory, folderName, filename, used, renames, destination => File.Copy(sourcePath, destination, true));
                }

                rewriteRenamedReferences(directory, renames);

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
            if (RootDirectory == null || !Directory.Exists(RootDirectory))
                return null;

            foreach (string directory in Directory.EnumerateDirectories(RootDirectory))
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
            || !Directory.Exists(Path.Combine(RootDirectory!, folderName));

        private static readonly HashSet<string> reserved_names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        private static void tryWriteRelativeFile(string directory, string skinName, string relativeName, HashSet<string> used, List<(string from, string to)> renames, Action<string> write)
        {
            string normalized = normalizeRelative(relativeName);
            string writable = writableRelative(normalized);
            string destination = Path.Combine(directory, writable.Replace('/', Path.DirectorySeparatorChar));

            if (!used.Add(destination))
            {
                string? folder = Path.GetDirectoryName(destination);
                string stem = Path.GetFileNameWithoutExtension(destination);
                string ext = Path.GetExtension(destination);
                string relativeDir = writable.Contains('/') ? writable[..writable.LastIndexOf('/')] : string.Empty;

                for (int i = 2; ; i++)
                {
                    string file = $"{stem}_{i}{ext}";
                    destination = string.IsNullOrEmpty(folder) ? file : Path.Combine(folder, file);

                    if (!used.Add(destination))
                        continue;

                    writable = string.IsNullOrEmpty(relativeDir) ? file : relativeDir + "/" + file;
                    break;
                }
            }

            try
            {
                string? parent = Path.GetDirectoryName(destination);

                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                write(destination);

                if (!string.Equals(normalized, writable, StringComparison.Ordinal))
                {
                    Logger.Log($"传统皮肤「{skinName}」的「{normalized}」已改名为「{writable}」。", LoggingTarget.Database, LogLevel.Important);
                    rememberRename(renames, normalized, writable);
                }
            }
            catch (Exception e)
            {
                Logger.Log($"传统皮肤「{skinName}」的「{normalized}」写入失败：{e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        private static string normalizeRelative(string relativeName)
        {
            var parts = new List<string>();

            foreach (string segment in relativeName.Split('/', '\\'))
            {
                if (segment.Length > 0)
                    parts.Add(segment);
            }

            return parts.Count == 0 ? "skin" : string.Join("/", parts);
        }

        private static string writableRelative(string normalized)
        {
            var parts = new List<string>();

            foreach (string segment in normalized.Split('/'))
                parts.Add(sanitizeSegment(segment));

            return string.Join("/", parts);
        }

        private static readonly string[] reference_extensions = { ".ini", ".json", ".txt", ".cfg" };

        private static void rememberRename(List<(string from, string to)> renames, string from, string to)
        {
            renames.Add((from, to));

            string[] fromParts = from.Split('/');
            string[] toParts = to.Split('/');
            int count = Math.Min(fromParts.Length, toParts.Length);

            for (int i = 0; i < count; i++)
            {
                if (string.Equals(fromParts[i], toParts[i], StringComparison.Ordinal))
                    continue;

                string prefixFrom = string.Join("/", fromParts.Take(i + 1));
                string prefixTo = string.Join("/", toParts.Take(i + 1));

                if (!renames.Exists(rename => rename.from == prefixFrom))
                    renames.Add((prefixFrom, prefixTo));

                break;
            }
        }

        // ASCII 路径按字节替换，避免把 Shift-JIS 的 skin.ini 整文件重编码。
        private static void rewriteRenamedReferences(string directory, List<(string from, string to)> renames)
        {
            renames.RemoveAll(rename => !isAscii(rename.from) || !isAscii(rename.to));

            if (renames.Count == 0)
                return;

            renames.Sort((a, b) => b.from.Length.CompareTo(a.from.Length));

            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (!reference_extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        continue;

                    byte[] data;

                    try
                    {
                        data = File.ReadAllBytes(path);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"无法读取 {path} 以更新文件名：{e.Message}", LoggingTarget.Database, LogLevel.Error);
                        continue;
                    }

                    byte[] updated = data;

                    foreach ((string from, string to) in renames)
                    {
                        updated = replaceAscii(updated, from, to);
                        updated = replaceAscii(updated, from.Replace('/', '\\'), to.Replace('/', '\\'));
                    }

                    if (updated == data)
                        continue;

                    try
                    {
                        File.WriteAllBytes(path, updated);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"无法写回 {path}：{e.Message}", LoggingTarget.Database, LogLevel.Error);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"更新皮肤内文件名引用失败：{e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        private static bool isAscii(string value)
        {
            foreach (char c in value)
            {
                if (c > 127)
                    return false;
            }

            return true;
        }

        private static byte[] replaceAscii(byte[] data, string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.Equals(from, to, StringComparison.Ordinal))
                return data;

            byte[] fromBytes = Encoding.ASCII.GetBytes(from);
            byte[] toBytes = Encoding.ASCII.GetBytes(to);

            if (fromBytes.Length == 0 || fromBytes.Length > data.Length)
                return data;

            bool found = false;

            for (int i = 0; i <= data.Length - fromBytes.Length; i++)
            {
                if (!matchesAt(data, i, fromBytes))
                    continue;

                found = true;
                break;
            }

            if (!found)
                return data;

            var output = new List<byte>(data.Length);

            for (int i = 0; i < data.Length;)
            {
                if (i <= data.Length - fromBytes.Length && matchesAt(data, i, fromBytes))
                {
                    output.AddRange(toBytes);
                    i += fromBytes.Length;
                }
                else
                {
                    output.Add(data[i]);
                    i++;
                }
            }

            return output.ToArray();
        }

        private static bool matchesAt(byte[] data, int index, byte[] needle)
        {
            for (int i = 0; i < needle.Length; i++)
            {
                if (data[index + i] != needle[i])
                    return false;
            }

            return true;
        }

        private static string sanitizeSegment(string segment)
        {
            string name = sanitize(segment);
            string stem = Path.GetFileNameWithoutExtension(name);

            if (reserved_names.Contains(stem))
                name = "_" + name;

            return name;
        }

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
            try
            {
                if (File.Exists(path) && File.ReadAllText(path) == contents)
                    return false;

                File.WriteAllText(path, contents, Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"无法写入 {path}。");
                return false;
            }
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
