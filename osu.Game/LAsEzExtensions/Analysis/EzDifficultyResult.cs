// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public readonly struct EzDifficultyResult
    {
        public readonly double AverageKps;
        public readonly double MaxKps;
        public readonly List<double> KpsList;
        public readonly Dictionary<int, int> ColumnCounts;
        public readonly Dictionary<int, int> HoldNoteCounts;
        public readonly string ScratchText;
        public readonly double? XxySr;

        public EzDifficultyResult(double averageKps,
                                  double maxKps,
                                  List<double> kpsList,
                                  Dictionary<int, int> columnCounts,
                                  Dictionary<int, int> holdNoteCounts,
                                  string scratchText,
                                  double? xxySr)
        {
            AverageKps = averageKps;
            MaxKps = maxKps;
            KpsList = kpsList;
            ColumnCounts = columnCounts;
            HoldNoteCounts = holdNoteCounts;
            ScratchText = scratchText;
            XxySr = xxySr;
        }
    }
}
