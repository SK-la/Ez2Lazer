// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion
{
    public class KrrOscillator
    {
        private readonly int maxValue;
        private int currentValue;
        private int direction;
        private readonly bool isSpecialCase;

        public KrrOscillator(int maxValue, Random? random = null)
        {
            if (maxValue < 0) throw new ArgumentException("maxValue 必须不小于零");

            this.maxValue = maxValue;

            if (maxValue == 0)
            {
                currentValue = 0;
                isSpecialCase = true;
            }
            else if (maxValue == 1)
            {
                Random rnd = random ?? new Random();
                currentValue = rnd.Next(0, 2);
                direction = rnd.Next(0, 2) == 0 ? -1 : 1;
                isSpecialCase = true;
            }
            else
            {
                Random rnd = random ?? new Random();
                currentValue = rnd.Next(1, maxValue);
                direction = rnd.Next(0, 2) == 0 ? -1 : 1;
                isSpecialCase = false;
            }
        }

        public int GetCurrent() => currentValue;

        public void Next()
        {
            if (isSpecialCase)
            {
                if (maxValue == 0)
                {
                    currentValue = 0;
                }
                else if (maxValue == 1)
                {
                    currentValue = 1 - currentValue;
                }
            }
            else
            {
                currentValue += direction;

                if (currentValue > maxValue)
                {
                    currentValue = maxValue - 1;
                    direction = -1;
                }
                else if (currentValue < 0)
                {
                    currentValue = 1;
                    direction = 1;
                }
            }
        }
    }
}
