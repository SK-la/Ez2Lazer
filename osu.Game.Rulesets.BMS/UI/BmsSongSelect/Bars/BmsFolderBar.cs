// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public sealed class BmsFolderBar : BmsDirectoryBar
    {
        public string Crc { get; }
        public string FullPath { get; }

        public BmsFolderBar(string crc, string name, string fullPath)
        {
            Crc = crc;
            FullPath = fullPath;
            Title = string.IsNullOrEmpty(name) ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : name;
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
        {
            var result = context.FolderTree.GetChildren(Crc).ToList();
            result.AddRange(context.BeatmapManager.GetChartsByFolder(FullPath).Select(chart => new BmsSongBar(chart)));
            return result;
        }
    }
}
