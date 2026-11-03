// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public sealed class BmsFolderTree
    {
        private readonly Dictionary<string, BmsFolderNode> nodesByCrc = new Dictionary<string, BmsFolderNode>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BmsRajaFolderRoot> roots = new List<BmsRajaFolderRoot>();

        public IReadOnlyList<BmsRajaFolderRoot> Roots => roots;

        public static BmsFolderTree Build(IReadOnlyList<string> libraryRoots)
        {
            var tree = new BmsFolderTree();
            var rootEntries = libraryRoots.Where(Directory.Exists).ToList();

            foreach (string root in rootEntries)
            {
                string rootCrc = BmsPathCrc.Compute(root);
                var rootNode = tree.getOrCreateNode(rootCrc, null, Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), root);
                tree.roots.Add(new BmsRajaFolderRoot(root, rootCrc, rootNode));
            }

            return tree;
        }

        public bool TryGetNode(string crc, out BmsFolderNode node) => nodesByCrc.TryGetValue(crc, out node!);

        public IReadOnlyList<BmsBar> GetRootBars()
        {
            var bars = new List<BmsBar>();

            foreach (var root in roots)
                bars.Add(new BmsFolderBar(root.Crc, root.DisplayName, root.Path));

            return bars;
        }

        public IReadOnlyList<BmsBar> GetChildren(string parentCrc)
        {
            if (!nodesByCrc.TryGetValue(parentCrc, out var node))
                return Array.Empty<BmsBar>();

            var result = new List<BmsBar>();

            foreach (var child in node.Children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                result.Add(new BmsFolderBar(child.Crc, child.Name, child.FullPath));

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
    }
}
