// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Resolves beatmap files exclusively from an external content root, using Realm storage paths and relative filenames.
    /// Does not consult the internal Realm file store.
    /// </summary>
    public sealed class ExternalBeatmapFileStore : IResourceStore<byte[]>
    {
        private readonly string contentRoot;
        private readonly Dictionary<string, string> storagePathToRelativeFilename;

        public ExternalBeatmapFileStore(string contentRoot, IEnumerable<(string storagePath, string relativeFilename)> fileMappings)
        {
            this.contentRoot = Path.GetFullPath(contentRoot);
            storagePathToRelativeFilename = fileMappings
                                            .Where(m => !string.IsNullOrEmpty(m.storagePath))
                                            .GroupBy(m => m.storagePath, StringComparer.OrdinalIgnoreCase)
                                            .ToDictionary(g => g.Key, g => g.First().relativeFilename, StringComparer.OrdinalIgnoreCase);
        }

        public byte[] Get(string name) => readBytes(openFromContentRoot(name)) ?? null!;

        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(readBytes(openFromContentRoot(name)) ?? null!);

        public Stream? GetStream(string name) => openFromContentRoot(name);

        public IEnumerable<string> GetAvailableResources() => storagePathToRelativeFilename.Keys;

        public void Dispose()
        {
        }

        private static byte[]? readBytes(Stream? stream)
        {
            if (stream == null)
                return null;

            using (stream)
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private Stream? openFromContentRoot(string name)
        {
            foreach (string candidate in getCandidateRelativePaths(name))
            {
                string fullPath = Path.Combine(contentRoot, candidate.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(fullPath))
                    return File.OpenRead(fullPath);
            }

            return null;
        }

        private IEnumerable<string> getCandidateRelativePaths(string name)
        {
            if (storagePathToRelativeFilename.TryGetValue(name, out string? mapped))
                yield return mapped;

            yield return name;

            string fileName = Path.GetFileName(name);

            if (!string.IsNullOrEmpty(fileName))
                yield return fileName;
        }
    }
}
