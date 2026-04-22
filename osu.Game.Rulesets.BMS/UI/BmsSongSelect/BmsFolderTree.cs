// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public sealed class BmsFolderTree
    {
        private readonly Dictionary<string, BmsFolderNode> nodesByCrc = new Dictionary<string, BmsFolderNode>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BmsRajaFolderRoot> roots = new List<BmsRajaFolderRoot>();

        public IReadOnlyList<BmsRajaFolderRoot> Roots => roots;

        public static BmsFolderTree Build(IReadOnlyList<string> libraryRoots, IEnumerable<BMSSongCache> songs)
        {
            var tree = new BmsFolderTree();
            var rootEntries = libraryRoots.Where(Directory.Exists).ToList();

            foreach (string root in rootEntries)
            {
                string rootCrc = BmsPathCrc.Compute(root);
                var rootNode = tree.getOrCreateNode(rootCrc, null, Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), root);
                tree.roots.Add(new BmsRajaFolderRoot(root, rootCrc, rootNode));
            }

            foreach (var song in songs)
            {
                string? matchedRoot = rootEntries.FirstOrDefault(r => song.FolderPath.StartsWith(r, StringComparison.OrdinalIgnoreCase));
                if (matchedRoot == null)
                    continue;

                string relative = Path.GetRelativePath(matchedRoot, song.FolderPath);
                string parentPath = matchedRoot;
                string parentCrc = BmsPathCrc.Compute(matchedRoot);

                if (!string.IsNullOrEmpty(relative) && relative != ".")
                {
                    foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    {
                        if (string.IsNullOrEmpty(segment))
                            continue;

                        parentPath = Path.Combine(parentPath, segment);
                        string crc = BmsPathCrc.Compute(parentPath);
                        tree.getOrCreateNode(crc, parentCrc, segment, parentPath);
                        parentCrc = crc;
                    }
                }

                if (tree.nodesByCrc.TryGetValue(parentCrc, out var leaf))
                    leaf.Songs.Add(song);
            }

            return tree;
        }

        public bool TryGetNode(string crc, out BmsFolderNode node) => nodesByCrc.TryGetValue(crc, out node!);

        public IReadOnlyList<BmsBar> GetRootBars()
        {
            var bars = new List<BmsBar>();

            foreach (var root in roots)
                bars.Add(new BmsFolderBar(root.Crc, root.DisplayName, root.Path, root.Node));

            return bars;
        }

        public IReadOnlyList<BmsBar> GetChildren(string parentCrc)
        {
            if (!nodesByCrc.TryGetValue(parentCrc, out var node))
                return Array.Empty<BmsBar>();

            var result = new List<BmsBar>();

            foreach (var child in node.Children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                result.Add(new BmsFolderBar(child.Crc, child.Name, child.FullPath, child));

            foreach (var song in node.Songs.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var chart in song.Charts.OrderBy(c => c.PlayLevel).ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase))
                    result.Add(new BmsSongBar(chart));
            }

            return result;
        }

        private BmsFolderNode getOrCreateNode(string crc, string? parentCrc, string name, string fullPath)
        {
            if (nodesByCrc.TryGetValue(crc, out var existing))
                return existing;

            var node = new BmsFolderNode(crc, parentCrc, name, fullPath);
            nodesByCrc[crc] = node;

            if (parentCrc != null && nodesByCrc.TryGetValue(parentCrc, out var parent))
                parent.Children.Add(node);

            return node;
        }
    }

    public sealed class BmsRajaFolderRoot
    {
        public BmsRajaFolderRoot(string path, string crc, BmsFolderNode node)
        {
            Path = path;
            Crc = crc;
            Node = node;
            DisplayName = string.IsNullOrEmpty(node.Name)
                ? System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
                : node.Name;
        }

        public string Path { get; }
        public string Crc { get; }
        public string DisplayName { get; }
        public BmsFolderNode Node { get; }
    }

    public sealed class BmsFolderNode(string crc, string? parentCrc, string name, string fullPath)
    {
        public string Crc { get; } = crc;
        public string? ParentCrc { get; } = parentCrc;
        public string Name { get; } = name;
        public string FullPath { get; } = fullPath;
        public List<BmsFolderNode> Children { get; } = new List<BmsFolderNode>();
        public List<BMSSongCache> Songs { get; } = new List<BMSSongCache>();
    }
}
