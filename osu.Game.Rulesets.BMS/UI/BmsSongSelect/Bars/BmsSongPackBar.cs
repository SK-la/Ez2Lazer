// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    /// <summary>
    /// A local library song: one pack folder with one or more chart files as difficulties.
    /// </summary>
    public sealed class BmsSongPackBar : BmsSelectableBar
    {
        public string FolderPath { get; }

        public IReadOnlyList<BmsChartSummary> Difficulties { get; }

        public BmsSongPackBar(string folderPath, string folderName, IReadOnlyList<BmsChartSummary> difficulties)
        {
            FolderPath = folderPath;
            Difficulties = difficulties
                           .OrderBy(d => d.PlayLevel)
                           .ThenBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
                           .ToList();

            BmsChartSummary primary = Difficulties[0];
            bool sharedTitle = Difficulties.Select(d => d.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                               && !string.IsNullOrWhiteSpace(primary.Title);

            Title = sharedTitle ? primary.Title : (string.IsNullOrEmpty(folderName) ? Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : folderName);
            Subtitle = $"{primary.Artist} / {Difficulties.Count} diffs";
        }

        public override string Title { get; }

        public override string Subtitle { get; }
    }
}
