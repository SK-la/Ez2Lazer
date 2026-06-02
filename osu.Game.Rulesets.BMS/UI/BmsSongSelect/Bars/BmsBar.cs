// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public abstract class BmsBar
    {
        public abstract string Title { get; }

        public virtual string Subtitle => string.Empty;

        public virtual bool IsSelectable => false;

        public virtual bool IsDirectory => false;
    }

    public abstract class BmsDirectoryBar : BmsBar
    {
        public override bool IsDirectory => true;

        public abstract bool IsSortable { get; }

        public abstract IReadOnlyList<BmsBar> GetChildren(BmsBarContext context);
    }

    public abstract class BmsSelectableBar : BmsBar
    {
        public override bool IsSelectable => true;
    }
}
