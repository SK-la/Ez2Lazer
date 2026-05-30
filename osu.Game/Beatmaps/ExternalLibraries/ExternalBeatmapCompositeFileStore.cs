// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Chains the game file store with an optional external content root, resolving storage paths and relative filenames.
    /// </summary>
    public sealed class ExternalBeatmapCompositeFileStore : IResourceStore<byte[]>
    {
        private readonly ResourceStore<byte[]> chain;
        private readonly string? contentRoot;
        private readonly Dictionary<string, string> storagePathToRelativeFilename;

        public ExternalBeatmapCompositeFileStore(IResourceStore<byte[]> primaryStore, string? contentRoot, IEnumerable<(string storagePath, string relativeFilename)> fileMappings)
        {
            this.contentRoot = string.IsNullOrWhiteSpace(contentRoot) ? null : Path.GetFullPath(contentRoot);
            storagePathToRelativeFilename = fileMappings
                                            .Where(m => !string.IsNullOrEmpty(m.storagePath))
                                            .GroupBy(m => m.storagePath, StringComparer.OrdinalIgnoreCase)
                                            .ToDictionary(g => g.Key, g => g.First().relativeFilename, StringComparer.OrdinalIgnoreCase);

            chain = new ResourceStore<byte[]>(primaryStore);

            if (this.contentRoot != null)
                chain.AddStore(new StorageBackedResourceStore(new NativeStorage(this.contentRoot)));
        }

        public byte[] Get(string name)
        {
            byte[]? result = chain.Get(name);
            return result ?? readBytes(openFromContentRoot(name)) ?? null!;
        }

        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            byte[]? result = chain.GetAsync(name, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            return Task.FromResult(result ?? readBytes(openFromContentRoot(name)) ?? null!);
        }

        public Stream? GetStream(string name)
        {
            Stream? result = chain.GetStream(name);
            return result ?? openFromContentRoot(name);
        }

        public IEnumerable<string> GetAvailableResources() => chain.GetAvailableResources();

        public void Dispose() => chain.Dispose();

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
            if (contentRoot == null)
                return null;

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
