// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    // TODO: 难度表 — 游戏内导入 .bmt、按表分组、IR 表同步

    public interface IBmsDifficultyTableProvider
    {
        IReadOnlyList<BmsTableBar> GetTables();
    }

    public sealed class BmsDifficultyTableRegistry : IBmsDifficultyTableProvider
    {
        public IReadOnlyList<BmsTableBar> GetTables() => Array.Empty<BmsTableBar>();
    }

    public sealed class BmsTableBar : BmsDirectoryBar
    {
        public BmsTableBar(string tableName)
        {
            Title = tableName;
        }

        public override string Title { get; }

        public override bool IsSortable => false;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
        {
            // TODO: 难度表 — HashBar 子项由 .bmt 解析生成
            return new BmsBar[] { new BmsPlaceholderBar("难度表功能尚未实现") };
        }
    }

    public sealed class BmsHashBar : BmsDirectoryBar
    {
        public BmsHashBar(string levelName)
        {
            Title = levelName;
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
        {
            // TODO: 难度表 — 按 sha256/md5 列表解析 SongBar
            return Array.Empty<BmsBar>();
        }
    }

    public sealed class BmsGradeBar : BmsSelectableBar
    {
        public BmsGradeBar(string courseName)
        {
            Title = courseName;
        }

        public override string Title { get; }
    }

    internal sealed class BmsPlaceholderBar : BmsBar
    {
        public BmsPlaceholderBar(string title) => Title = title;
        public override string Title { get; }
    }
}
