// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public enum BmsSortMode
    {
        Title,
        Level,
        Artist,
        Folder,
    }

    public sealed class BmsSortPolicy
    {
        public BmsSortMode Mode { get; private set; } = BmsSortMode.Title;

        public void CycleNext()
        {
            Mode = (BmsSortMode)(((int)Mode + 1) % Enum.GetValues<BmsSortMode>().Length);
        }

        public IReadOnlyList<BmsBar> Sort(IReadOnlyList<BmsBar> bars)
        {
            IEnumerable<BmsBar> folders = bars.Where(b => b.IsDirectory);
            IEnumerable<BmsBar> songs = bars.Where(b => b is BmsSongBar);

            songs = Mode switch
            {
                BmsSortMode.Level => songs.OrderBy(b => ((BmsSongBar)b).Chart.PlayLevel).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                BmsSortMode.Artist => songs.OrderBy(b => ((BmsSongBar)b).Chart.Artist, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                BmsSortMode.Folder => songs.OrderBy(b => ((BmsSongBar)b).Chart.FolderPath, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                _ => songs.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
            };

            return folders.Concat(songs).ToList();
        }
    }
}
