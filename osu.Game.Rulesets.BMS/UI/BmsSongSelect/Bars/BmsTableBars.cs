// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public interface IBmsDifficultyTableProvider
    {
        IReadOnlyList<BmsTableBar> GetTables();
    }

    public sealed class BmsEmptyDifficultyTableProvider : IBmsDifficultyTableProvider
    {
        public IReadOnlyList<BmsTableBar> GetTables() => Array.Empty<BmsTableBar>();
    }

    public sealed class BmsDifficultyTableRegistry : IBmsDifficultyTableProvider
    {
        private readonly BmsDifficultyTableStore store;

        public BmsDifficultyTableRegistry(BmsDifficultyTableStore store)
        {
            this.store = store;
        }

        public IReadOnlyList<BmsTableBar> GetTables()
            => store.GetTables().Select(t => new BmsTableBar(t)).ToList();
    }

    public sealed class BmsTableBar : BmsDirectoryBar
    {
        public BmsDifficultyTable Table { get; }

        public BmsTableBar(BmsDifficultyTable table)
        {
            Table = table;
            Title = table.Name;
        }

        public override string Title { get; }

        public override string Subtitle => Table.Tag;

        public override bool IsSortable => false;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
            => Table.Levels.Select(level => (BmsBar)new BmsHashBar(Table, level)).ToList();
    }

    public sealed class BmsHashBar : BmsDirectoryBar
    {
        public BmsDifficultyTable Table { get; }

        public BmsTableLevel Level { get; }

        public BmsHashBar(BmsDifficultyTable table, BmsTableLevel level)
        {
            Table = table;
            Level = level;
            Title = level.Name;
        }

        public override string Title { get; }

        public override string Subtitle => $"{Level.Entries.Count} charts";

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
        {
            var bars = new List<BmsBar>(Level.Entries.Count);

            foreach (BmsTableEntry entry in Level.Entries)
            {
                if (tryResolve(context, entry, out BmsChartSummary summary))
                    bars.Add(new BmsSongBar(summary));
                else
                    bars.Add(new BmsMissingChartBar(entry, Level.Name));
            }

            return bars;
        }

        private static bool tryResolve(BmsBarContext context, BmsTableEntry entry, out BmsChartSummary summary)
        {
            summary = null!;

            if (!string.IsNullOrEmpty(entry.Sha256)
                && context.BeatmapManager.TryGetChartSummaryByContentHash(entry.Sha256, out summary))
                return true;

            if (!string.IsNullOrEmpty(entry.Md5)
                && context.BeatmapManager.TryGetChartSummaryByContentHash(entry.Md5, out summary))
                return true;

            return false;
        }
    }

    public sealed class BmsMissingChartBar : BmsBar
    {
        public BmsTableEntry Entry { get; }

        public string TableLevel { get; }

        public BmsMissingChartBar(BmsTableEntry entry, string tableLevel)
        {
            Entry = entry;
            TableLevel = tableLevel;
            Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.PreferredHash : entry.Title;
            Subtitle = string.IsNullOrWhiteSpace(entry.Artist) ? "未导入" : $"{entry.Artist} / 未导入";
        }

        public override string Title { get; }

        public override string Subtitle { get; }
    }

    public sealed class BmsGradeBar : BmsSelectableBar
    {
        public BmsGradeBar(string courseName)
        {
            Title = courseName;
        }

        public override string Title { get; }
    }
}
