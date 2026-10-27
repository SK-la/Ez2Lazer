// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;

namespace osu.Game.Beatmaps.Formats
{
    /// <summary>
    /// Helper methods to parse from string to number and perform very basic validation.
    /// </summary>
    public static class Parsing
    {
        public const int MAX_COORDINATE_VALUE = 131072;

        public const double MAX_PARSE_VALUE = int.MaxValue;

        public static float ParseFloat(string input, float parseLimit = (float)MAX_PARSE_VALUE, bool allowNaN = false)
            => ParseFloat(input.AsSpan(), parseLimit, allowNaN);

        public static float ParseFloat(ReadOnlySpan<char> input, float parseLimit = (float)MAX_PARSE_VALUE, bool allowNaN = false)
        {
            float output = parseFloat(input);

            if (output < -parseLimit) throw new OverflowException("Value is too low");
            if (output > parseLimit) throw new OverflowException("Value is too high");

            if (!allowNaN && float.IsNaN(output)) throw new FormatException("Not a number");

            return output;
        }

        public static double ParseDouble(string input, double parseLimit = MAX_PARSE_VALUE, bool allowNaN = false)
            => ParseDouble(input.AsSpan(), parseLimit, allowNaN);

        public static double ParseDouble(ReadOnlySpan<char> input, double parseLimit = MAX_PARSE_VALUE, bool allowNaN = false)
        {
            double output = parseDouble(input);

            if (output < -parseLimit) throw new OverflowException("Value is too low");
            if (output > parseLimit) throw new OverflowException("Value is too high");

            if (!allowNaN && double.IsNaN(output)) throw new FormatException("Not a number");

            return output;
        }

        public static int ParseInt(string input, int parseLimit = (int)MAX_PARSE_VALUE)
            => ParseInt(input.AsSpan(), parseLimit);

        public static int ParseInt(ReadOnlySpan<char> input, int parseLimit = (int)MAX_PARSE_VALUE)
        {
            int output = int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);

            if (output < -parseLimit) throw new OverflowException("Value is too low");
            if (output > parseLimit) throw new OverflowException("Value is too high");

            return output;
        }

        /// <summary>
        /// 解析 float，无需 <see cref="float.Parse(string, IFormatProvider)"/> 所需的中间字符串。
        /// </summary>
        /// <remarks>
        /// 提供 span 重载是因为谱面解析对每个控制点都会调用；aspire 谱面可能有数百万个，
        /// 切 span 几乎无开销，而分配子字符串既费时又费内存。
        /// </remarks>
        private static float parseFloat(ReadOnlySpan<char> input)
        {
            // 谱面坐标绝大多数是纯整数。将此类整数转为 float 的舍入与按位解析一致，
            // 捷径不会改变任何值，并避开在千万控制点路径上主导解码时间的通用解析器。
            if (tryParseInteger(input, out long integer))
                return integer;

            return float.Parse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        }

        private static bool tryParseInteger(ReadOnlySpan<char> input, out long value)
        {
            value = 0;

            if (input.IsEmpty)
                return false;

            bool negative = input[0] == '-';
            int index = negative ? 1 : 0;

            if (index == input.Length)
                return false;

            for (; index < input.Length; index++)
            {
                uint digit = (uint)(input[index] - '0');

                if (digit > 9)
                    return false;

                if (value > (long.MaxValue - digit) / 10)
                    return false;

                value = value * 10 + digit;
            }

            if (negative)
                value = -value;

            return true;
        }

        private static double parseDouble(ReadOnlySpan<char> input)
            => double.Parse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
    }
}
