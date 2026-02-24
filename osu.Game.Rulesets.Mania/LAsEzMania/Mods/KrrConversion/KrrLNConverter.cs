using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public static class KrrLNConverter
    {
        public static void Transform(ManiaBeatmap beatmap, KrrLNOptions options)
        {
            var rg = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();

            int cs = Math.Max(1, beatmap.TotalColumns);
            List<ManiaHitObject> maniaObjects = beatmap.HitObjects.ToList();

            (NoteMatrix matrix1, List<int> timeAxis1) = KrrN2NcConverter.BuildMatrix(beatmap, maniaObjects, cs, false);

            NoteMatrix processedMatrix = buildAndProcessMatrix(matrix1, timeAxis1, beatmap, options, rg, maniaObjects);
            applyChangesToHitObjects(beatmap, processedMatrix, maniaObjects, options);
        }

        private static NoteMatrix buildAndProcessMatrix(NoteMatrix matrix1,
                                                        List<int> timeAxis1,
                                                        ManiaBeatmap beatmap,
                                                        KrrLNOptions options,
                                                        Random rg,
                                                        List<ManiaHitObject> maniaObjects)
        {
            int cs = matrix1.Cols;
            int rows = matrix1.Rows;

            NoteMatrix availableTimeMtx = generateAvailableTimeMatrix(matrix1, timeAxis1);
            var longLnWaitModify = new NoteMatrix(rows, cs);
            var shortLnWaitModify = new NoteMatrix(rows, cs);
            DoubleMatrix beatLengthMtx = generateBeatLengthMatrix(matrix1, maniaObjects, beatmap);
            BoolMatrix orgIsLnMatrix = generateOrgIsLn(matrix1, maniaObjects);

            if (!options.ProcessOriginalIsChecked)
                markOriginalLnAsSkipped(matrix1, orgIsLnMatrix);

            int borderKey = options.LengthThreshold;
            var borderdrict = new BeatNumberGenerator(64, 1.0 / 4);
            var shortLnDrict = new BeatNumberGenerator(256, 1.0 / 16);

            (BoolMatrix shortLnFlag, BoolMatrix longLnFlag) = generateLnFlags(matrix1, availableTimeMtx, beatLengthMtx, borderdrict, borderKey);

            longLnFlag = markByPercentage(longLnFlag, options.LongPercentage, rg);
            shortLnFlag = markByPercentage(shortLnFlag, options.ShortPercentage, rg);
            longLnFlag = limitTruePerRow(longLnFlag, options.LongLimit, rg);
            shortLnFlag = limitTruePerRow(shortLnFlag, options.ShortLimit, rg);

            double longLevel = options.LongLevel;
            double shortLevel = shortLnDrict.GetValue(options.ShortLevel);

            generateLongLnMatrix(matrix1, longLnWaitModify, longLnFlag, availableTimeMtx, beatLengthMtx, borderKey, longLevel, shortLevel, options.LongRandom, borderdrict, rg);
            generateShortLnMatrix(matrix1, shortLnWaitModify, shortLnFlag, availableTimeMtx, beatLengthMtx, borderKey, shortLevel, options.ShortRandom, borderdrict, rg);

            NoteMatrix result = mergeMatrices(longLnWaitModify, shortLnWaitModify);

            // Alignment is applied to normal notes; LNAlignment is applied to hold notes when writing notes.

            return result;
        }

        private static void applyChangesToHitObjects(ManiaBeatmap beatmap, NoteMatrix mergeMtx, List<ManiaHitObject> maniaObjects, KrrLNOptions options)
        {
            int cs = mergeMtx.Cols;
            (NoteMatrix matrix2, List<int> _) = KrrN2NcConverter.BuildMatrix(beatmap, maniaObjects, cs, false);

            Span<int> mergeSpan = mergeMtx.AsSpan();
            Span<int> matrix2Span = matrix2.AsSpan();

            for (int i = 0; i < mergeSpan.Length; i++)
            {
                int length = mergeSpan[i];
                if (length < 0) continue;

                int index = matrix2Span[i];
                if (index < 0 || index >= beatmap.HitObjects.Count) continue;

                var original = beatmap.HitObjects[index];
                int alignment = original is HoldNote ? options.LNAlignment : options.Alignment;
                double adjustedLength = applyLnAlignment(length, original.StartTime, beatmap, alignment);
                double newEnd = original.StartTime + adjustedLength;

                if (original is HoldNote hold)
                {
                    hold.EndTime = newEnd;
                }
                else
                {
                    var newHold = createHoldFrom(original, newEnd);
                    beatmap.HitObjects[index] = newHold;
                }
            }
        }

        private static double applyLnAlignment(int length, double startTime, ManiaBeatmap beatmap, int lnAlignment)
        {
            if (lnAlignment <= 0) return length;

            var alignList = new Dictionary<int, double>
            {
                { 1, 1.0 / 8 },
                { 2, 1.0 / 7 },
                { 3, 1.0 / 6 },
                { 4, 1.0 / 5 },
                { 5, 1.0 / 4 },
                { 6, 1.0 / 3 },
                { 7, 1.0 / 2 },
                { 8, 1.0 }
            };

            if (!alignList.TryGetValue(lnAlignment, out double alignValue)) return length;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength;
            double denominator = beatLength * alignValue;

            if (denominator <= 0) return length;

            const double epsilon = 0.99;
            return (int)((int)((length + epsilon) / denominator) * denominator);
        }

        private static HoldNote createHoldFrom(ManiaHitObject original, double endTime)
        {
            var newHold = new HoldNote
            {
                StartTime = original.StartTime,
                Column = original.Column,
                Samples = original.Samples?.ToList(),
                EndTime = endTime
            };

            return newHold;
        }

        private static NoteMatrix generateAvailableTimeMatrix(NoteMatrix matrix1, List<int> timeAxis1)
        {
            var availableTimeMtx = new NoteMatrix(matrix1.Rows, matrix1.Cols);
            int lastTime = timeAxis1.LastOrDefault();

            Span<int> matrixSpan = matrix1.AsSpan();
            Span<int> timeMtxSpan = availableTimeMtx.AsSpan();

            int rows = matrix1.Rows;
            int cols = matrix1.Cols;

            for (int j = 0; j < cols; j++)
            {
                int currentRow = -1;
                int colOffset = j;

                for (int i = 0; i < rows; i++)
                {
                    int index = (i * cols) + colOffset;

                    if (matrixSpan[index] >= 0)
                    {
                        if (currentRow >= 0)
                        {
                            int nextRow = i;
                            int availableTime = timeAxis1[nextRow] - timeAxis1[currentRow];
                            timeMtxSpan[(currentRow * cols) + colOffset] = availableTime;
                        }

                        currentRow = i;
                    }
                }

                if (currentRow >= 0)
                {
                    int availableTime = lastTime - timeAxis1[currentRow];
                    timeMtxSpan[(currentRow * cols) + colOffset] = availableTime;
                }
            }

            return availableTimeMtx;
        }

        private static DoubleMatrix generateBeatLengthMatrix(NoteMatrix matrix1, List<ManiaHitObject> maniaObjects, ManiaBeatmap beatmap)
        {
            var beatLengthMtx = new DoubleMatrix(matrix1.Rows, matrix1.Cols);
            Span<double> beatLengthSpan = beatLengthMtx.AsSpan();
            Span<int> matrixSpan = matrix1.AsSpan();

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int index = matrixSpan[i];

                if (index >= 0 && index < maniaObjects.Count)
                {
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(maniaObjects[index].StartTime).BeatLength;
                    beatLengthSpan[i] = beatLength;
                }
            }

            return beatLengthMtx;
        }

        private static BoolMatrix generateOrgIsLn(NoteMatrix matrix1, List<ManiaHitObject> maniaObjects)
        {
            int cols = matrix1.Cols;
            var orgIsLn = new BoolMatrix(matrix1.Rows, cols);
            Span<bool> orgIsLnSpan = orgIsLn.AsSpan();
            Span<int> matrixSpan = matrix1.AsSpan();

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int index = matrixSpan[i];
                if (index < 0 || index >= maniaObjects.Count) continue;

                if (maniaObjects[index] is HoldNote hold && hold.EndTime > hold.StartTime)
                    orgIsLnSpan[i] = true;
            }

            return orgIsLn;
        }

        private static void markOriginalLnAsSkipped(NoteMatrix matrix, BoolMatrix orgIsLnMatrix)
        {
            Span<int> matrixSpan = matrix.AsSpan();
            Span<bool> orgIsLnSpan = orgIsLnMatrix.AsSpan();

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (orgIsLnSpan[i] && matrixSpan[i] >= 0)
                    matrixSpan[i] = NoteMatrix.EMPTY;
            }
        }

        private static (BoolMatrix shortLnFlag, BoolMatrix longLnFlag) generateLnFlags(NoteMatrix matrix1,
                                                                                       NoteMatrix availableTimeMtx,
                                                                                       DoubleMatrix beatLengthMtx,
                                                                                       BeatNumberGenerator bg,
                                                                                       int borderKey)
        {
            var shortLnFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);
            var longLnFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);

            Span<int> matrixSpan = matrix1.AsSpan();
            Span<int> availableTimeSpan = availableTimeMtx.AsSpan();
            Span<double> beatLengthSpan = beatLengthMtx.AsSpan();
            Span<bool> shortLnSpan = shortLnFlag.AsSpan();
            Span<bool> longLnSpan = longLnFlag.AsSpan();

            double borderValue = bg.GetValue(borderKey);

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int index = matrixSpan[i];

                if (index >= 0)
                {
                    if (availableTimeSpan[i] > borderValue * beatLengthSpan[i])
                        longLnSpan[i] = true;
                    else
                        shortLnSpan[i] = true;
                }
            }

            return (shortLnFlag, longLnFlag);
        }

        private static NoteMatrix mergeMatrices(NoteMatrix matrix1, NoteMatrix matrix2)
        {
            if (matrix1.Rows != matrix2.Rows || matrix1.Cols != matrix2.Cols)
                throw new ArgumentException("Matrix dimensions must match");

            int rows = matrix1.Rows;
            int cols = matrix1.Cols;
            var result = new NoteMatrix(rows, cols);

            Span<int> span1 = matrix1.AsSpan();
            Span<int> span2 = matrix2.AsSpan();
            Span<int> resultSpan = result.AsSpan();

            for (int i = 0; i < span1.Length; i++)
            {
                int val1 = span1[i];
                int val2 = span2[i];

                if (val1 >= NoteMatrix.EMPTY && val2 >= NoteMatrix.EMPTY)
                    resultSpan[i] = Math.Max(val1, val2);
                else if (val1 >= NoteMatrix.EMPTY)
                    resultSpan[i] = val1;
                else if (val2 >= NoteMatrix.EMPTY)
                    resultSpan[i] = val2;
                else
                    resultSpan[i] = NoteMatrix.EMPTY;
            }

            return result;
        }

        private static BoolMatrix markByPercentage(BoolMatrix source, double percentage, Random random)
        {
            if (percentage >= 100) return source;
            if (percentage <= 0) return new BoolMatrix(source.Rows, source.Cols);

            var result = new BoolMatrix(source.Rows, source.Cols);
            Span<bool> sourceSpan = source.AsSpan();
            Span<bool> resultSpan = result.AsSpan();

            sourceSpan.CopyTo(resultSpan);

            int cols = source.Cols;
            var truePositions = new List<(int row, int col)>();

            for (int i = 0; i < sourceSpan.Length; i++)
            {
                if (sourceSpan[i])
                {
                    int row = i / cols;
                    int col = i % cols;
                    truePositions.Add((row, col));
                }
            }

            if (truePositions.Count == 0)
                return new BoolMatrix(source.Rows, source.Cols);

            double ratio = 1.0 - (percentage / 100.0);
            int countToSetFalse = (int)Math.Round(truePositions.Count * ratio);

            var groupedByRow = truePositions.GroupBy(pos => pos.row)
                                            .ToDictionary(g => g.Key, g => g.ToList());

            var positionsToSetFalse = new HashSet<(int, int)>();

            foreach (var group in groupedByRow)
            {
                List<(int row, int col)> positionsInRow = group.Value;
                int countInRow = (int)Math.Round(positionsInRow.Count * ratio);
                countInRow = Math.Min(countInRow, positionsInRow.Count);

                IEnumerable<(int row, int col)> selectedInRow = positionsInRow.OrderBy(_ => random.Next())
                                                                              .Take(countInRow);
                foreach ((int row, int col) pos in selectedInRow) positionsToSetFalse.Add(pos);
            }

            if (positionsToSetFalse.Count < countToSetFalse)
            {
                IEnumerable<(int row, int col)> remaining = truePositions.Where(pos => !positionsToSetFalse.Contains(pos))
                                                                         .OrderBy(_ => random.Next())
                                                                         .Take(countToSetFalse - positionsToSetFalse.Count);

                foreach ((int row, int col) pos in remaining) positionsToSetFalse.Add(pos);
            }

            foreach ((int row, int col) in positionsToSetFalse)
                resultSpan[(row * cols) + col] = false;

            return result;
        }

        private static BoolMatrix limitTruePerRow(BoolMatrix source, int limit, Random random)
        {
            int rows = source.Rows;
            int cols = source.Cols;

            var result = new BoolMatrix(rows, cols);
            Span<bool> sourceSpan = source.AsSpan();
            Span<bool> resultSpan = result.AsSpan();

            for (int i = 0; i < rows; i++)
            {
                var truePositions = new List<int>();

                for (int j = 0; j < cols; j++)
                {
                    if (sourceSpan[(i * cols) + j])
                        truePositions.Add(j);
                }

                if (truePositions.Count > limit)
                {
                    List<int> shuffledPositions = truePositions.OrderBy(_ => random.Next()).ToList();
                    for (int k = 0; k < limit; k++)
                        resultSpan[(i * cols) + shuffledPositions[k]] = sourceSpan[(i * cols) + shuffledPositions[k]];
                }
                else
                {
                    for (int j = 0; j < cols; j++)
                        resultSpan[(i * cols) + j] = sourceSpan[(i * cols) + j];
                }
            }

            return result;
        }

        private static void generateLongLnMatrix(NoteMatrix matrix1,
                                                 NoteMatrix longLnWaitModify,
                                                 BoolMatrix longLnFlag,
                                                 NoteMatrix availableTimeMtx,
                                                 DoubleMatrix beatLengthMtx,
                                                 int borderKey,
                                                 double longLevel,
                                                 double shortLevel,
                                                 int longRandom,
                                                 BeatNumberGenerator bg,
                                                 Random random)
        {
            Span<int> matrixSpan = matrix1.AsSpan();
            Span<bool> longFlagSpan = longLnFlag.AsSpan();
            Span<int> availableTimeSpan = availableTimeMtx.AsSpan();
            Span<double> beatLengthSpan = beatLengthMtx.AsSpan();
            Span<int> resultSpan = longLnWaitModify.AsSpan();

            double borderValue = bg.GetValue(borderKey);

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (longFlagSpan[i])
                {
                    double mean = availableTimeSpan[i] * longLevel / 100;
                    double di = borderValue * beatLengthSpan[i];
                    int newLength;

                    if (mean < di)
                    {
                        newLength = generateRandom(0, di, shortLevel * beatLengthSpan[i], longRandom, random);
                    }
                    else
                    {
                        newLength = generateRandom(di, availableTimeSpan[i], mean, longRandom, random);
                    }

                    if (newLength > availableTimeSpan[i] - 34)
                        newLength = availableTimeSpan[i] - 34;

                    resultSpan[i] = newLength;
                }
            }
        }

        private static void generateShortLnMatrix(NoteMatrix matrix1,
                                                  NoteMatrix shortLnWaitModify,
                                                  BoolMatrix shortLnFlag,
                                                  NoteMatrix availableTimeMtx,
                                                  DoubleMatrix beatLengthMtx,
                                                  int borderKey,
                                                  double shortLevel,
                                                  int shortRandom,
                                                  BeatNumberGenerator bg,
                                                  Random random)
        {
            Span<int> matrixSpan = matrix1.AsSpan();
            Span<bool> shortFlagSpan = shortLnFlag.AsSpan();
            Span<int> availableTimeSpan = availableTimeMtx.AsSpan();
            Span<double> beatLengthSpan = beatLengthMtx.AsSpan();
            Span<int> resultSpan = shortLnWaitModify.AsSpan();

            double borderValue = bg.GetValue(borderKey);

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (shortFlagSpan[i])
                {
                    int newLength = generateRandom(
                        0,
                        borderValue * beatLengthSpan[i],
                        shortLevel * beatLengthSpan[i],
                        shortRandom,
                        random
                    );

                    if (newLength > availableTimeSpan[i] - 34)
                        newLength = availableTimeSpan[i] - 34;

                    resultSpan[i] = newLength;
                }
            }
        }

        private static int generateRandom(double d, double u, double m, int p, Random r)
        {
            if (p <= 0) return (int)m;

            if (p >= 100) p = 100;

            double pRatio = p / 100.0;
            double d2 = m - ((m - d) * pRatio);
            double u2 = m + ((u - m) * pRatio);

            d2 = Math.Max(d2, d);
            u2 = Math.Min(u2, u);

            if (d2 >= u2)
                return (int)m;

            double u1 = r.NextDouble();
            double u2R = r.NextDouble();
            double betaRandom = (u1 + u2R) / 2.0;

            double range = u2 - d2;
            double mRelative = (m - d2) / range;

            double result;
            if (betaRandom <= 0.5)
                result = d2 + (mRelative * betaRandom / 0.5 * range);
            else
                result = d2 + ((mRelative + ((1 - mRelative) * (betaRandom - 0.5) / 0.5)) * range);

            return (int)result;
        }
    }

    internal class BeatNumberGenerator
    {
        private readonly double[] values;
        private readonly double lastValue;

        public BeatNumberGenerator(int middleIndex, double coefficient, double lastValue = 999.0)
        {
            this.lastValue = lastValue;
            values = new double[middleIndex + 2];
            values[0] = 0.0;
            for (int i = 1; i <= middleIndex; i++) values[i] = i * coefficient;
            values[middleIndex + 1] = lastValue;
        }

        public double GetValue(int index)
        {
            if (index <= 0) return 0;
            if (index >= values.Length - 1) return lastValue;

            return values[index];
        }
    }
}
