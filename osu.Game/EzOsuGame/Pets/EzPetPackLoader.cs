// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Pets
{
    public class EzPetPack
    {
        public required string Name { get; init; }

        public required EzPetPackDefinition Definition { get; init; }

        /// <summary>
        /// Clip ids that have at least one frame file on disk.
        /// </summary>
        public required IReadOnlySet<string> AvailableClips { get; init; }

        public bool IsDefault => string.Equals(Name, EzDefaultPetPack.NAME, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads <c>pet.json</c> from <see cref="EzModifyPath.PETS_PATH"/>.
    /// </summary>
    public class EzPetPackLoader
    {
        public const string MANIFEST_FILE = "pet.json";

        private readonly Storage petsStorage;

        public EzPetPackLoader(Storage gameStorage)
        {
            petsStorage = gameStorage.GetStorageForDirectory(EzModifyPath.PETS_PATH);
        }

        public IReadOnlyList<string> ListPackNames()
        {
            EnsureDefaultPack();

            var names = new List<string>();

            foreach (string dir in petsStorage.GetDirectories(string.Empty))
            {
                string name = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                name = Path.GetFileName(name);

                if (string.IsNullOrEmpty(name))
                    continue;

                if (petsStorage.Exists(Path.Combine(name, MANIFEST_FILE)))
                    names.Add(name);
            }

            if (!names.Contains(EzDefaultPetPack.NAME, StringComparer.OrdinalIgnoreCase))
                names.Insert(0, EzDefaultPetPack.NAME);

            return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public EzPetPack? Load(string packName)
        {
            EnsureDefaultPack();

            if (string.IsNullOrWhiteSpace(packName))
                packName = EzDefaultPetPack.NAME;

            string manifestPath = Path.Combine(packName, MANIFEST_FILE);

            if (!petsStorage.Exists(manifestPath))
            {
                if (string.Equals(packName, EzDefaultPetPack.NAME, StringComparison.OrdinalIgnoreCase))
                    return createDefaultPackInMemory();

                Logger.Log($"Ez pet pack '{packName}' is missing {MANIFEST_FILE}.", LoggingTarget.Runtime);
                return null;
            }

            try
            {
                using var stream = petsStorage.GetStream(manifestPath);
                if (stream == null)
                    return null;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var definition = EzPetPackDefinition.Parse(reader.ReadToEnd());
                var available = resolveAvailableClips(packName, definition);
                return new EzPetPack
                {
                    Name = packName,
                    Definition = definition,
                    AvailableClips = available,
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez pet pack '{packName}'");
                return null;
            }
        }

        public void EnsureDefaultPack()
        {
            string manifestPath = Path.Combine(EzDefaultPetPack.NAME, MANIFEST_FILE);

            if (petsStorage.Exists(manifestPath))
                return;

            try
            {
                petsStorage.GetStorageForDirectory(EzDefaultPetPack.NAME);

                using var stream = petsStorage.CreateFileSafely(manifestPath);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(EzDefaultPetPack.PET_JSON);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write default Ez pet pack");
            }
        }

        public IReadOnlyList<string> GetClipFrameNames(string packName, string clipName, EzPetClipDefinition clip)
        {
            string? folder = resolveClipFolder(packName, clipName, clip);
            if (folder == null)
                return [];

            string directory = Path.Combine(packName, folder);

            IEnumerable<string> files;

            try
            {
                files = petsStorage.GetFiles(directory);
            }
            catch (Exception)
            {
                return [];
            }

            var indexed = EzPetFramePath.CollectIndexedFrameNames(files);
            var relative = new List<string>(indexed.Count);

            foreach (string fileName in indexed)
                relative.Add($"{folder}/{fileName}");

            return relative;
        }

        private HashSet<string> resolveAvailableClips(string packName, EzPetPackDefinition definition)
        {
            var available = new HashSet<string>(StringComparer.Ordinal);

            foreach ((string clipName, var clip) in definition.Clips)
            {
                if (GetClipFrameNames(packName, clipName, clip).Count > 0)
                    available.Add(clipName);
            }

            return available;
        }

        private string? resolveClipFolder(string packName, string clipName, EzPetClipDefinition clip)
        {
            foreach (string candidate in enumerateFolderCandidates(clipName, clip))
            {
                string? actual = findExistingDirectory(packName, candidate);
                if (actual != null)
                    return actual;
            }

            return null;
        }

        private static IEnumerable<string> enumerateFolderCandidates(string clipName, EzPetClipDefinition clip)
        {
            if (!string.IsNullOrWhiteSpace(clip.Folder))
                yield return clip.Folder.Trim();

            if (!string.IsNullOrWhiteSpace(clip.Frames) && clip.Frames.IndexOf('{') < 0)
                yield return clip.Frames.Trim();

            if (string.IsNullOrWhiteSpace(clipName))
                yield break;

            yield return clipName;

            string snake = EzPetFramePath.ToSnakeCase(clipName);
            if (!string.Equals(snake, clipName, StringComparison.OrdinalIgnoreCase))
                yield return snake;
        }

        private string? findExistingDirectory(string packName, string folderName)
        {
            string direct = Path.Combine(packName, folderName);
            if (petsStorage.ExistsDirectory(direct))
                return folderName;

            if (!petsStorage.ExistsDirectory(packName))
                return null;

            try
            {
                foreach (string dir in petsStorage.GetDirectories(packName))
                {
                    string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.Equals(name, folderName, StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static EzPetPack createDefaultPackInMemory()
        {
            var definition = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);
            return new EzPetPack
            {
                Name = EzDefaultPetPack.NAME,
                Definition = definition,
                AvailableClips = new HashSet<string>(StringComparer.Ordinal),
            };
        }
    }
}
