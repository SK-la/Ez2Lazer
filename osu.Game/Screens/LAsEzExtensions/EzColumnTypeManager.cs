// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.LAsEzExtensions
{
    public static class EzColumnTypeManager
    {
        public static EzColumnType GetColumnType(int keyMode, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= keyMode)
                return EzColumnType.A;

            if (isSpecialColumn(keyMode, columnIndex))
                return EzColumnType.S;

            if (isEffectColumn(keyMode, columnIndex))
                return EzColumnType.E;

            if (isPanelColumn(keyMode, columnIndex))
                return EzColumnType.P;

            int normalKeyIndex = 0;

            for (int i = 0; i < columnIndex; i++)
            {
                if (!isSpecialColumn(keyMode, i) && !isPanelColumn(keyMode, i))
                    normalKeyIndex++;
            }

            return getNormalColumnType(keyMode, normalKeyIndex, columnIndex);
        }

        private static EzColumnType getNormalColumnType(int keyMode, int normalKeyIndex, int columnIndex)
        {
            if (keyMode % 2 == 0)
            {
                int halfKey = keyMode / 2;
                return columnIndex < halfKey
                    ? (normalKeyIndex % 2 == 0 ? EzColumnType.A : EzColumnType.B)
                    : (normalKeyIndex % 2 == 0 ? EzColumnType.B : EzColumnType.A);
            }

            return normalKeyIndex % 2 == 0 ? EzColumnType.A : EzColumnType.B;
        }

        private static bool isSpecialColumn(int keyMode, int columnIndex)
        {
            return keyMode switch
            {
                12 when columnIndex is 0 or 11 => true,
                14 when columnIndex is 0 or 12 => true,
                16 when columnIndex is 0 or 15 => true,
                _ => false
            };
        }

        public static bool IsSpecialColumn(int keyMode, int columnIndex)
        {
            return isSpecialColumn(keyMode, columnIndex);
        }

        private static bool isEffectColumn(int keyMode, int columnIndex)
        {
            return keyMode switch
            {
                16 when columnIndex is 6 or 7 or 8 or 9 => true,
                _ => false
            };
        }

        private static bool isPanelColumn(int keyMode, int columnIndex)
        {
            return keyMode switch
            {
                5 when columnIndex == 2 => true,
                7 when columnIndex == 3 => true,
                9 when columnIndex == 4 => true,
                14 when columnIndex is 6 => true,
                _ => false
            };
        }
    }
}
