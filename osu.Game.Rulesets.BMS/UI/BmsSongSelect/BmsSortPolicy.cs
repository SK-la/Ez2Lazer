// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;
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

        public BmsChartSort IndexSort => (BmsChartSort)Mode;

        public void CycleNext()
        {
            Mode = (BmsSortMode)(((int)Mode + 1) % Enum.GetValues<BmsSortMode>().Length);
        }

        public IReadOnlyList<BmsBar> Sort(IReadOnlyList<BmsBar> bars)
        {
            IEnumerable<BmsBar> folders = bars.Where(b => b.IsDirectory);
            IEnumerable<BmsBar> songs = bars.Where(b => b is BmsSongBar or BmsMissingChartBar or BmsSongPackBar);

            songs = Mode switch
            {
                BmsSortMode.Level => songs.OrderBy(b => b switch
                {
                    BmsSongBar song => song.Summary.PlayLevel,
                    BmsSongPackBar pack => pack.Difficulties[0].PlayLevel,
                    _ => 0,
                }).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                BmsSortMode.Artist => songs.OrderBy(b => b switch
                {
                    BmsSongBar song => song.Summary.Artist,
                    BmsSongPackBar pack => pack.Difficulties[0].Artist,
                    _ => b.Subtitle,
                }, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                BmsSortMode.Folder => songs.OrderBy(b => b switch
                {
                    BmsSongBar song => song.Summary.FolderPath,
                    BmsSongPackBar pack => pack.FolderPath,
                    _ => string.Empty,
                }, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                _ => songs.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
            };

            return folders.Concat(songs).ToList();
        }
    }
}
