// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public enum BmsKeyMode
    {
        Any = 0,
        Keys5 = 5,
        Keys7 = 7,
        Keys10 = 10,
        Keys14 = 14,
    }

    public sealed class BmsRajaKeyModeFilter
    {
        private static readonly BmsKeyMode[] cycle =
        {
            BmsKeyMode.Any,
            BmsKeyMode.Keys5,
            BmsKeyMode.Keys7,
            BmsKeyMode.Keys10,
            BmsKeyMode.Keys14,
        };

        public BmsKeyMode Current { get; private set; } = BmsKeyMode.Any;

        public void CycleNext()
        {
            int index = Array.IndexOf(cycle, Current);
            Current = cycle[(index + 1) % cycle.Length];
        }

        public bool Matches(int keyCount)
        {
            return Current switch
            {
                BmsKeyMode.Any => true,
                BmsKeyMode.Keys5 => keyCount == 5 || keyCount == 8,
                BmsKeyMode.Keys7 => keyCount == 7 || keyCount == 9,
                BmsKeyMode.Keys10 => keyCount == 10,
                BmsKeyMode.Keys14 => keyCount == 14,
                _ => true,
            };
        }
    }
}
