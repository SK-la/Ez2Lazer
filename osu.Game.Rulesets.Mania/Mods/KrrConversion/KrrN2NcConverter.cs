using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using osu.Framework.Logging;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public static class KrrN2NcConverter
    {
        internal static (NoteMatrix matrix, List<int> timeAxis) BuildMatrix(ManiaBeatmap beatmap, List<ManiaHitObject> notes, int cols, bool expandHoldBody)
            => buildMatrix(beatmap, notes, cols, expandHoldBody);

        internal static Span<double> GenerateBeatLengthAxis(Span<int> timeAxis, List<ManiaHitObject> maniaObjects, ManiaBeatmap beatmap)
            => generateBeatLengthAxis(timeAxis, maniaObjects, beatmap);

        internal static Span<int> GenerateEndTimeIndex(List<ManiaHitObject> maniaObjects)
            => generateEndTimeIndex(maniaObjects);

        internal static Span<int> GenerateOrgColIndex(NoteMatrix matrix)
            => generateOrgColIndex(matrix);

        internal static NoteMatrix DoKeys(NoteMatrix matrix,
                                          Span<int> endTimeIndexAxis,
                                          Span<int> timeAxis,
                                          Span<double> beatlengthAxis,
                                          Span<int> orgColIndex,
                                          int originalKeys,
                                          int targetKeys,
                                          int maxKeys,
                                          int minKeys,
                                          double convertTime,
                                          Random random)
            => doKeys(matrix, endTimeIndexAxis, timeAxis, beatlengthAxis, orgColIndex, originalKeys, targetKeys, maxKeys, minKeys, convertTime, random);

        public static void Transform(ManiaBeatmap beatmap, KrrOptions? options)
        {
            int targetKeys = options?.TargetKeys ?? beatmap.TotalColumns;
            int maxKeys = options?.MaxKeys ?? targetKeys;
            int minKeys = options?.MinKeys ?? 3;
            int speedIndex = options?.BeatSpeed ?? 4;
            int seedValue = options?.Seed ?? KrrConversionHelper.ComputeSeedFromBeatmap(beatmap);

            int originalKeys = (int)beatmap.BeatmapInfo.Difficulty.CircleSize;
            if (originalKeys == targetKeys) return;

            var rng = new Random(seedValue);

            double bpm = beatmap.BeatmapInfo.BPM;

            if (bpm <= 0)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(0).BeatLength;
                bpm = beatLength > 0 ? 60000.0 / beatLength : 180;
            }

            double convertTime = KrrConversionHelper.ComputeConvertTime(speedIndex, bpm);

            List<ManiaHitObject> notes = beatmap.HitObjects.ToList();

            bool expandHoldBody = targetKeys - originalKeys < 0;
            (NoteMatrix matrix, List<int> timeAxisTemp) = buildMatrix(beatmap, notes, originalKeys, expandHoldBody);
            Span<int> timeAxis = CollectionsMarshal.AsSpan(timeAxisTemp);

            NoteMatrix processedMatrix = processMatrix(matrix, timeAxis, beatmap, notes, originalKeys, targetKeys, maxKeys, minKeys, convertTime, rng);
            applyChangesToHitObjects(beatmap, notes, processedMatrix);
        }

        private static NoteMatrix processMatrix(NoteMatrix matrix,
                                                Span<int> timeAxis,
                                                ManiaBeatmap beatmap,
                                                List<ManiaHitObject> notes,
                                                int originalKeys,
                                                int targetKeys,
                                                int maxKeys,
                                                int minKeys,
                                                double convertTime,
                                                Random random)
        {
            Span<double> beatLengthAxis = generateBeatLengthAxis(timeAxis, notes, beatmap);
            Span<int> endTimeIndexAxis = generateEndTimeIndex(notes);
            Span<int> orgColIndex = generateOrgColIndex(matrix);

            return doKeys(matrix, endTimeIndexAxis, timeAxis, beatLengthAxis, orgColIndex, originalKeys, targetKeys, maxKeys, minKeys, convertTime, random);
        }

        private static void applyChangesToHitObjects(ManiaBeatmap beatmap, List<ManiaHitObject> notes, NoteMatrix processedMatrix)
        {
            var newObjects = new List<ManiaHitObject>();
            int targetKeys = processedMatrix.Cols;
            Span<int> matrixSpan = processedMatrix.AsSpan();

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int oldIndex = matrixSpan[i];
                int col = i % targetKeys;

                if (oldIndex >= 0 && oldIndex < notes.Count)
                {
                    var original = notes[oldIndex];
                    newObjects.Add(KrrConversionHelper.CloneWithColumn(original, col));
                }
            }

            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column));
        }

        private static (NoteMatrix, List<int>) buildMatrix(ManiaBeatmap beatmap, List<ManiaHitObject> notes, int cols, bool expandHoldBody)
        {
            // getMTXandTimeAxis(): unique start times only, ordered
            List<int> uniqueStartTimes = notes
                                         .Select(n => toTimeKey(n.StartTime))
                                         .Distinct()
                                         .OrderBy(t => t)
                                         .ToList();

            int timeCount = uniqueStartTimes.Count;
            var matrix = new NoteMatrix(timeCount, cols);

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                int timeIndex = uniqueStartTimes.IndexOf(toTimeKey(note.StartTime));
                int colIndex = Math.Clamp(note.Column, 0, cols - 1);

                if (timeIndex >= 0)
                    matrix[timeIndex, colIndex] = i;
            }

            if (!expandHoldBody)
                return (matrix, uniqueStartTimes);

            return expandHoldBodyMatrix(matrix, uniqueStartTimes, notes, cols);
        }

        private static (NoteMatrix, List<int>) expandHoldBodyMatrix(NoteMatrix matrix, List<int> timeAxis, List<ManiaHitObject> notes, int cols)
        {
            var allTimes = new HashSet<int>(timeAxis);
            foreach (int endTime in getEndTimeList(notes))
                allTimes.Add(endTime);

            var newTimeAxis = allTimes.OrderBy(t => t).ToList();
            var newMatrix = new NoteMatrix(newTimeAxis.Count, cols);

            int oldTimeIndex = 0;
            int newTimeIndex = 0;

            while (newTimeIndex < newTimeAxis.Count && oldTimeIndex < timeAxis.Count)
            {
                if (newTimeAxis[newTimeIndex] == timeAxis[oldTimeIndex])
                {
                    for (int col = 0; col < matrix.Cols; col++)
                        newMatrix[newTimeIndex, col] = matrix[oldTimeIndex, col];

                    oldTimeIndex++;
                    newTimeIndex++;
                }
                else if (newTimeAxis[newTimeIndex] < timeAxis[oldTimeIndex])
                {
                    for (int col = 0; col < matrix.Cols; col++)
                        newMatrix[newTimeIndex, col] = NoteMatrix.EMPTY;

                    newTimeIndex++;
                }
                else
                {
                    oldTimeIndex++;
                }
            }

            while (newTimeIndex < newTimeAxis.Count)
            {
                for (int col = 0; col < matrix.Cols; col++)
                    newMatrix[newTimeIndex, col] = NoteMatrix.EMPTY;

                newTimeIndex++;
            }

            foreach (var note in notes)
            {
                if (note is HoldNote hold && hold.EndTime > hold.StartTime)
                {
                    int startIndex = newTimeAxis.IndexOf(toTimeKey(hold.StartTime));
                    int endIndex = newTimeAxis.IndexOf(toTimeKey(hold.EndTime));
                    int colIndex = Math.Clamp(hold.Column, 0, cols - 1);

                    for (int i = startIndex + 1; i <= endIndex; i++)
                    {
                        if (newMatrix[i, colIndex] == NoteMatrix.EMPTY)
                            newMatrix[i, colIndex] = NoteMatrix.HOLD_BODY;
                    }
                }
            }

            return (newMatrix, newTimeAxis);
        }

        private static List<int> getEndTimeList(List<ManiaHitObject> notes)
        {
            var uniqueEndTimes = new HashSet<int>();

            foreach (var note in notes)
            {
                if (note is HoldNote hold)
                    uniqueEndTimes.Add(toTimeKey(hold.EndTime));
                else
                    uniqueEndTimes.Add(toTimeKey(note.StartTime));
            }

            return uniqueEndTimes.OrderBy(t => t).ToList();
        }

        private static int toTimeKey(double time)
            => (int)Math.Round(time);

        private static Span<double> generateBeatLengthAxis(Span<int> timeAxis, List<ManiaHitObject> maniaObjects, ManiaBeatmap beatmap)
        {
            List<double> result1 = Enumerable.Repeat(-1.0, timeAxis.Length).ToList();

            var sortedNotes = maniaObjects
                              .Select((note, index) => new { Note = note, Index = index })
                              .OrderBy(x => x.Note.StartTime)
                              .ToList();

            int currentTimeAxisIndex = 0;

            foreach (var item in sortedNotes)
            {
                int startTime = toTimeKey(item.Note.StartTime);

                while (currentTimeAxisIndex < timeAxis.Length &&
                       timeAxis[currentTimeAxisIndex] < startTime)
                    currentTimeAxisIndex++;

                if (currentTimeAxisIndex < timeAxis.Length && timeAxis[currentTimeAxisIndex] == startTime)
                    result1[currentTimeAxisIndex] = beatmap.ControlPointInfo.TimingPointAt(item.Note.StartTime).BeatLength;
            }

            return CollectionsMarshal.AsSpan(result1);
        }

        private static Span<int> generateEndTimeIndex(List<ManiaHitObject> maniaObjects)
        {
            var result = new List<int>();
            foreach (var item in maniaObjects)
                result.Add(toTimeKey(item is HoldNote h ? h.EndTime : item.StartTime));

            return CollectionsMarshal.AsSpan(result);
        }

        private static Span<int> generateOrgColIndex(NoteMatrix matrix)
        {
            int cols = matrix.Cols;
            Span<int> matrixSpan = matrix.AsSpan();

            int maxIndex = -1;
            for (int i = 0; i < matrixSpan.Length; i++)
                maxIndex = Math.Max(maxIndex, matrixSpan[i]);

            if (maxIndex < 0)
                return CollectionsMarshal.AsSpan(new List<int>());

            var orgColIndex = new int[maxIndex + 1];
            Array.Fill(orgColIndex, -1);

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int noteIndex = matrixSpan[i];
                if (noteIndex >= 0 && noteIndex < orgColIndex.Length)
                    orgColIndex[noteIndex] = i % cols;
            }

            return orgColIndex;
        }

        private static NoteMatrix doKeys(NoteMatrix matrix,
                                         Span<int> endTimeIndexAxis,
                                         Span<int> timeAxis,
                                         Span<double> beatlengthAxis,
                                         Span<int> orgColIndex,
                                         int originalKeys,
                                         int targetKeys,
                                         int maxKeys,
                                         int minKeys,
                                         double convertTime,
                                         Random random)
        {
            int turn = targetKeys - originalKeys;

            if (turn >= 0)
            {
                bool maxKeysEqualOriginal = maxKeys == originalKeys;
                (NoteMatrix oldMtx, NoteMatrix insertMtx) = convertMtx(turn, timeAxis, convertTime, originalKeys, random, maxKeysEqualOriginal);
                NoteMatrix newMatrix = convert(matrix, endTimeIndexAxis, oldMtx, insertMtx, orgColIndex, timeAxis, targetKeys, beatlengthAxis, maxKeysEqualOriginal);
                densityReducer(newMatrix, maxKeys, minKeys, targetKeys, random);
                return newMatrix;
            }
            else
            {
                NoteMatrix newMatrix = smartReduceColumns(matrix, timeAxis, -turn, convertTime, beatlengthAxis, random);
                densityReducer(newMatrix, maxKeys, minKeys, targetKeys, random);
                return newMatrix;
            }
        }

        private static (NoteMatrix, NoteMatrix) convertMtx(int turn,
                                                           Span<int> timeAxis,
                                                           double convertTime,
                                                           int originalKeys,
                                                           Random random,
                                                           bool ifMaxKeysEqual = false)
        {
            int rows = timeAxis.Length;

            if (rows == 0)
                throw new ArgumentException("行或者列为0，无法创建convert矩阵.");

            var oldMtx = new NoteMatrix(rows, turn);
            var insertMtx = new NoteMatrix(rows, turn);

            if (!ifMaxKeysEqual)
            {
                for (int col = 0; col < turn; col++)
                {
                    var oldIndex = new OscillatorGenerator(originalKeys - 1, random);
                    double timeCounter = 0;
                    int lastTime = timeAxis[0];

                    for (int row = 0; row < rows; row++)
                    {
                        oldMtx[row, col] = oldIndex.GetCurrent();

                        timeCounter += timeAxis[row] - lastTime;
                        lastTime = timeAxis[row];

                        if (timeCounter >= convertTime)
                        {
                            oldIndex.Next();
                            timeCounter = 0;
                        }
                    }

                    int randomMoves = random.Next(0, Math.Max(1, originalKeys - 1));
                    for (int i = 0; i < randomMoves; i++) oldIndex.Next();
                }
            }

            for (int col = 0; col < turn; col++)
            {
                var insertIndex = new OscillatorGenerator(originalKeys + col, random);
                double timeCounter = 0;
                int lastTime = timeAxis[0];

                for (int row = 0; row < rows; row++)
                {
                    insertMtx[row, col] = insertIndex.GetCurrent();

                    timeCounter += timeAxis[row] - lastTime;
                    lastTime = timeAxis[row];

                    if (timeCounter >= convertTime)
                    {
                        insertIndex.Next();
                        timeCounter = 0;
                    }
                }

                int randomMoves = random.Next(0, Math.Max(1, originalKeys - 1 + col));
                for (int i = 0; i < randomMoves; i++) insertIndex.Next();
            }

            return (oldMtx, insertMtx);
        }

        private static NoteMatrix convert(NoteMatrix matrix,
                                          Span<int> endTimeIndexAxis,
                                          NoteMatrix oldMtx,
                                          NoteMatrix insertMtx,
                                          Span<int> orgColIndex,
                                          Span<int> timeAxis,
                                          int targetKeys,
                                          Span<double> beatLengthAxis,
                                          bool maxKeysEqualTargetKeys)
        {
            try
            {
                int rows = matrix.Rows;
                int originalCols = matrix.Cols;
                int turn = oldMtx.Cols;
                NoteMatrix newMatrix = performInitialConvert(matrix, oldMtx, insertMtx, targetKeys, turn, rows, originalCols, maxKeysEqualTargetKeys);
                BoolMatrix mark = generateDeleteMark(newMatrix, timeAxis, endTimeIndexAxis, beatLengthAxis, orgColIndex, targetKeys);
                applyPositionBasedDeletion(newMatrix, mark);
                return newMatrix;
            }
            catch (Exception ex)
            {
                Logger.Log($"[KrrN2NcConverter] Convert failed: {ex.Message}");
                throw;
            }
        }

        private static NoteMatrix performInitialConvert(NoteMatrix matrix, NoteMatrix oldMtx, NoteMatrix insertMtx, int targetKeys, int turn, int rows, int originalCols, bool ifMaxKeysEqual)
        {
            var newMatrix = new NoteMatrix(rows, targetKeys);

            for (int i = 0; i < rows; i++)
            {
                int[] tempRow = new int[targetKeys];
                for (int k = 0; k < targetKeys; k++) tempRow[k] = NoteMatrix.EMPTY;

                Span<int> orgCurrentRow = matrix.GetRowSpan(i);
                for (int j = 0; j < originalCols && j < targetKeys; j++) tempRow[j] = orgCurrentRow[j];

                if (!ifMaxKeysEqual)
                {
                    for (int j = 0; j < turn; j++)
                    {
                        int oldIndex = oldMtx[i, j];
                        int insertIndex = insertMtx[i, j];
                        shiftInsert(tempRow, insertIndex);

                        if (oldIndex >= 0 && matrix[i, oldIndex] >= 0)
                            tempRow[insertIndex] = matrix[i, oldIndex];
                    }
                }
                else
                {
                    for (int j = 0; j < turn; j++)
                    {
                        int insertIndex = insertMtx[i, j];
                        shiftInsert(tempRow, insertIndex);
                    }
                }

                for (int j = 0; j < targetKeys; j++) newMatrix[i, j] = tempRow[j];
            }

            return newMatrix;
        }

        private static BoolMatrix generateDeleteMark(NoteMatrix newMatrix, Span<int> timeAxis, Span<int> endTimeIndexAxis, Span<double> beatLengthAxis, Span<int> orgColIndexAxis, int targetKeys)
        {
            var mark = new BoolMatrix(newMatrix.Rows, newMatrix.Cols);
            Span<bool> markSpan = mark.AsSpan();
            Span<int> newMatrixSpan = newMatrix.AsSpan();
            var endTimeTempRow = new Span<int>(new int[targetKeys]);
            var convertTimePointRow = new Span<int>(new int[targetKeys]);
            var orgColIndexRow = new Span<int>(new int[targetKeys]);
            convertTimePointRow.Fill(timeAxis[0]);
            orgColIndexRow.Fill(-1);

            for (int i = targetKeys; i < newMatrixSpan.Length; i++)
            {
                int oldIndex = newMatrixSpan[i];
                int preRowI = i - targetKeys;
                int preOldIndex = newMatrixSpan[preRowI];
                int row = i / targetKeys;
                int col = i % targetKeys;
                double space = beatLengthAxis[row - 1] / 4;

                if (preOldIndex >= 0)
                    endTimeTempRow[col] = Math.Max(endTimeIndexAxis[preOldIndex], endTimeTempRow[col]);

                if (timeAxis[row] < endTimeTempRow[col] + space - 10)
                    markSpan[i] = true;

                if (oldIndex >= 0 && orgColIndexAxis[oldIndex] != orgColIndexRow[col])
                {
                    orgColIndexRow[col] = orgColIndexAxis[oldIndex];
                    convertTimePointRow[col] = timeAxis[row - 1];
                }

                if (timeAxis[row] < convertTimePointRow[col] + space + 10)
                    markSpan[i] = true;
            }

            return mark;
        }

        private static void applyPositionBasedDeletion(NoteMatrix newMatrix, BoolMatrix mark)
        {
            Span<int> newMatrixSpan = newMatrix.AsSpan();
            Span<bool> markSpan = mark.AsSpan();

            for (int i = 0; i < newMatrixSpan.Length; i++)
            {
                if (markSpan[i])
                    newMatrixSpan[i] = NoteMatrix.EMPTY;
            }
        }

        private static void shiftInsert<T>(T nums, int insertIndex) where T : IList<int>
        {
            if (insertIndex >= 0 && insertIndex <= nums.Count - 1)
            {
                for (int i = nums.Count - 1; i > insertIndex; i--)
                    nums[i] = nums[i - 1];

                nums[insertIndex] = NoteMatrix.EMPTY;
            }
            else
                throw new ArgumentOutOfRangeException(nameof(insertIndex), "insertIndex 超出有效范围");
        }

        private static void densityReducer(NoteMatrix matrix, int maxKeys, int minKeys, int targetKeys, Random random)
        {
            int maxToRemovePerRow = targetKeys - maxKeys;
            if (maxToRemovePerRow <= 0) return;

            int rows = matrix.Rows;
            int cols = matrix.Cols;

            int[] columnDeletionCounts = new int[cols];

            for (int i = 0; i < rows; i++)
            {
                var activeNotes = new List<int>();

                for (int j = 0; j < cols; j++)
                {
                    if (matrix[i, j] >= 0)
                        activeNotes.Add(j);
                }

                if (activeNotes.Count <= minKeys) continue;

                int targetNotes = Math.Max(
                    minKeys,
                    Math.Min(
                        activeNotes.Count,
                        (int)(activeNotes.Count * (double)(targetKeys - maxToRemovePerRow) / targetKeys)
                    )
                );

                int toRemove = Math.Max(0, activeNotes.Count - targetNotes);
                if (toRemove <= 0) continue;

                var candidates = new List<int>(activeNotes);

                for (int r = 0; r < toRemove && candidates.Count > 0; r++)
                {
                    double[] weights = new double[candidates.Count];
                    double totalWeight = 0;

                    for (int j = 0; j < candidates.Count; j++)
                    {
                        weights[j] = 1.0 / (1.0 + columnDeletionCounts[candidates[j]]);
                        totalWeight += weights[j];
                    }

                    double randomValue = random.NextDouble() * totalWeight;
                    double currentWeight = 0;
                    int selectedIndex = 0;

                    for (int j = 0; j < candidates.Count; j++)
                    {
                        currentWeight += weights[j];

                        if (randomValue <= currentWeight)
                        {
                            selectedIndex = j;
                            break;
                        }
                    }

                    int columnToRemove = candidates[selectedIndex];
                    matrix[i, columnToRemove] = NoteMatrix.EMPTY;
                    columnDeletionCounts[columnToRemove]++;
                    candidates.RemoveAt(selectedIndex);
                }
            }
        }

        private static NoteMatrix smartReduceColumns(NoteMatrix orgMtx,
                                                     Span<int> timeAxis,
                                                     int turn,
                                                     double convertTime,
                                                     Span<double> beatLengthAxis,
                                                     Random random)
        {
            int rows = orgMtx.Rows;
            int originalCols = orgMtx.Cols;
            int targetCols = originalCols - turn;

            var newMatrix = new NoteMatrix(rows, targetCols);
            var originColumnMap = new NoteMatrix(rows, targetCols);

            int regionStart = 0;

            for (int regionEnd = 1; regionEnd < rows; regionEnd++)
            {
                bool isRegionEnd = timeAxis[regionEnd] - timeAxis[regionStart] >= convertTime;
                bool isLastRow = regionEnd == rows - 1;

                if (isRegionEnd || isLastRow)
                {
                    if (isLastRow && !isRegionEnd) regionEnd = rows - 1;

                    processRegion(orgMtx, newMatrix, originColumnMap, timeAxis, regionStart, regionEnd, targetCols, beatLengthAxis, random);
                    regionStart = regionEnd;
                }
            }

            if (regionStart < rows - 1)
                processRegion(orgMtx, newMatrix, originColumnMap, timeAxis, regionStart, rows - 1, targetCols, beatLengthAxis, random);

            processEmptyRows(orgMtx, newMatrix, timeAxis, beatLengthAxis, random);

            return newMatrix;
        }

        private static void processRegion(NoteMatrix orgMtx,
                                          NoteMatrix newMatrix,
                                          NoteMatrix originColumnMap,
                                          Span<int> timeAxis,
                                          int regionStart,
                                          int regionEnd,
                                          int targetCols,
                                          Span<double> beatLengthAxis,
                                          Random random)
        {
            int originalCols = orgMtx.Cols;
            int rows = orgMtx.Rows;

            int[] columnWeights = new int[originalCols];

            for (int i = regionStart; i <= regionEnd; i++)
            {
                for (int j = 0; j < originalCols; j++)
                {
                    if (orgMtx[i, j] >= 0)
                        columnWeights[j]++;
                }
            }

            List<int> columnsToRemove = getColumnsToRemove(columnWeights, targetCols, originalCols, orgMtx, regionStart, regionEnd);
            applyRandomAdjustmentToColumnSelection(columnsToRemove, columnWeights, originalCols, random);
            int[] columnMapping = createColumnMapping(originalCols, columnsToRemove);

            for (int row = regionStart; row <= regionEnd; row++)
            {
                for (int col = 0; col < originalCols; col++)
                {
                    int newValue = orgMtx[row, col];

                    if (newValue >= 0)
                    {
                        int newCol = columnMapping[col];

                        if (newCol >= 0)
                        {
                            if (isPositionAvailable(newMatrix, originColumnMap, col, row, newCol, timeAxis, beatLengthAxis[row]))
                            {
                                newMatrix[row, newCol] = newValue;
                                originColumnMap[row, newCol] = col;
                                copyLongNoteBody(orgMtx, newMatrix, row, col, newCol, rows);
                            }
                        }
                    }
                }
            }

            for (int row = regionStart; row <= regionEnd; row++)
                handleLongNoteExtensions(newMatrix, row, targetCols);

            applyMinimumNotesConstraint(newMatrix, orgMtx, regionStart, regionEnd, targetCols, timeAxis, beatLengthAxis, random);
        }

        private static void applyRandomAdjustmentToColumnSelection(List<int> columnsToRemove, int[] columnWeights, int originalCols, Random random)
        {
            for (int i = 0; i < columnsToRemove.Count; i++)
            {
                if (random.NextDouble() < 0.25)
                {
                    int currentCol = columnsToRemove[i];
                    int currentWeight = columnWeights[currentCol];

                    var candidates = new List<int>();

                    for (int col = 0; col < originalCols; col++)
                    {
                        if (!columnsToRemove.Contains(col) && Math.Abs(columnWeights[col] - currentWeight) <= 1)
                            candidates.Add(col);
                    }

                    if (candidates.Count > 0)
                    {
                        int replacement = candidates[random.Next(candidates.Count)];
                        columnsToRemove[i] = replacement;
                    }
                }
            }
        }

        private static void applyMinimumNotesConstraint(NoteMatrix matrix, NoteMatrix orgMtx, int startRow, int endRow, int targetCols, Span<int> timeAxis, Span<double> beatLengthAxis, Random random)
        {
            for (int row = startRow; row <= endRow; row++)
            {
                bool hasNote = false;

                for (int col = 0; col < targetCols; col++)
                {
                    if (matrix[row, col] >= 0)
                    {
                        hasNote = true;
                        break;
                    }
                }

                if (!hasNote)
                {
                    var candidateNotes = new List<int>();
                    int originalCols = orgMtx.Cols;

                    for (int col = 0; col < originalCols; col++)
                    {
                        if (orgMtx[row, col] >= 0)
                            candidateNotes.Add(col);
                    }

                    if (candidateNotes.Count > 0)
                    {
                        int selectedOrgCol = candidateNotes[random.Next(candidateNotes.Count)];

                        var availablePositions = new List<int>();

                        for (int col = 0; col < targetCols; col++)
                        {
                            if (isPositionAvailableForEmptyRow(matrix, timeAxis, row, col, beatLengthAxis[row]))
                            {
                                if (!isHoldNoteTailTooClose(matrix, orgMtx, timeAxis, row, selectedOrgCol, col, beatLengthAxis[row]))
                                    availablePositions.Add(col);
                            }
                        }

                        if (availablePositions.Count > 0)
                        {
                            int targetCol = availablePositions[random.Next(availablePositions.Count)];
                            matrix[row, targetCol] = orgMtx[row, selectedOrgCol];
                        }
                    }
                }
            }
        }

        private static List<int> getColumnsToRemove(int[] columnWeights,
                                                    int targetCols,
                                                    int originalCols,
                                                    NoteMatrix orgMtx,
                                                    int regionStart,
                                                    int regionEnd)
        {
            int colsToRemove = originalCols - targetCols;
            if (colsToRemove <= 0) return new List<int>();

            var columnList = new List<(int index, int weight, double risk)>();

            for (int i = 0; i < originalCols; i++)
            {
                int weight = columnWeights[i];
                double risk = calculateColumnRisk(orgMtx, i, originalCols, regionStart, regionEnd);
                columnList.Add((i, weight, risk));
            }

            columnList.Sort((a, b) =>
            {
                int weightComparison = a.weight.CompareTo(b.weight);
                if (weightComparison != 0)
                    return weightComparison;

                return a.risk.CompareTo(b.risk);
            });

            return columnList.Take(colsToRemove).Select(x => x.index).ToList();
        }

        private static double calculateColumnRisk(NoteMatrix matrix,
                                                  int colIndex,
                                                  int totalCols,
                                                  int regionStart,
                                                  int regionEnd)
        {
            int totalRows = 0;
            int emptyRows = 0;

            for (int row = regionStart; row <= regionEnd; row++)
            {
                bool hasNoteInRow = false;

                for (int c = 0; c < totalCols; c++)
                {
                    if (c != colIndex && matrix[row, c] >= 0)
                    {
                        hasNoteInRow = true;
                        break;
                    }
                }

                if (!hasNoteInRow && matrix[row, colIndex] >= 0) emptyRows++;
                totalRows++;
            }

            if (totalRows == 0) return 0;

            return (double)emptyRows / totalRows;
        }

        private static int[] createColumnMapping(int originalCols, List<int> columnsToRemove)
        {
            int[] mapping = new int[originalCols];
            int newColIndex = 0;

            for (int oldCol = 0; oldCol < originalCols; oldCol++)
            {
                if (!columnsToRemove.Contains(oldCol))
                    mapping[oldCol] = newColIndex++;
                else
                    mapping[oldCol] = -1;
            }

            return mapping;
        }

        private static bool isPositionAvailable(NoteMatrix matrix, NoteMatrix originColumnMap, int oldcol, int row, int col, Span<int> timeAxis, double beatLength)
        {
            if (matrix[row, col] != NoteMatrix.EMPTY)
                return false;

            for (int r = Math.Max(0, row - 3); r < row; r++)
            {
                if (timeAxis[row] - timeAxis[r] <= (beatLength / 2.5) + 10 && originColumnMap[r, col] != oldcol)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == NoteMatrix.HOLD_BODY)
                        return false;
                }
            }

            int rows = matrix.Rows;

            for (int r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
            {
                if (timeAxis[r] - timeAxis[row] <= (beatLength / 2.5) + 10 && originColumnMap[r, col] != oldcol)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == NoteMatrix.HOLD_BODY)
                        return false;
                }
            }

            return true;
        }

        private static void handleLongNoteExtensions(NoteMatrix newMatrix, int row, int targetCols)
        {
            for (int col = 0; col < targetCols; col++)
            {
                if (newMatrix[row, col] == NoteMatrix.EMPTY)
                {
                    if (row > 0 && newMatrix[row - 1, col] == NoteMatrix.HOLD_BODY)
                        newMatrix[row, col] = NoteMatrix.HOLD_BODY;
                }
            }
        }

        private static void copyLongNoteBody(NoteMatrix orgMtx,
                                             NoteMatrix newMatrix,
                                             int startRow,
                                             int oldCol,
                                             int newCol,
                                             int totalRows)
        {
            int row = startRow + 1;

            while (row < totalRows && orgMtx[row, oldCol] == NoteMatrix.HOLD_BODY)
            {
                if (newCol < newMatrix.Cols) newMatrix[row, newCol] = NoteMatrix.HOLD_BODY;
                row++;
            }
        }

        private static void processEmptyRows(NoteMatrix orgMtx,
                                             NoteMatrix newMatrix,
                                             Span<int> timeAxis,
                                             Span<double> beatLengthAxis,
                                             Random random)
        {
            int rows = newMatrix.Rows;
            int targetCols = newMatrix.Cols;
            int originalCols = orgMtx.Cols;

            for (int row = 0; row < rows; row++)
            {
                bool isEmptyRow = true;

                for (int col = 0; col < targetCols; col++)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        isEmptyRow = false;
                        break;
                    }
                }

                if (isEmptyRow)
                {
                    if (tryInsertNoteDirectly(newMatrix, orgMtx, timeAxis, row, targetCols, originalCols, beatLengthAxis[row], random))
                        continue;

                    tryClearSpaceAndInsert(orgMtx, newMatrix, timeAxis, row, targetCols, originalCols, beatLengthAxis[row], random);
                }
            }
        }

        private static bool tryInsertNoteDirectly(NoteMatrix newMatrix,
                                                  NoteMatrix orgMtx,
                                                  Span<int> timeAxis,
                                                  int row,
                                                  int targetCols,
                                                  int originalCols,
                                                  double beatLength,
                                                  Random random)
        {
            var availableCols = new List<int>();

            for (int col = 0; col < targetCols; col++)
            {
                if (isPositionAvailableForEmptyRow(newMatrix, timeAxis, row, col, beatLength))
                    availableCols.Add(col);
            }

            if (availableCols.Count == 0)
                return false;

            var candidateNotes = new List<(int orgCol, int noteIndex)>();

            for (int orgCol = 0; orgCol < originalCols; orgCol++)
            {
                if (orgMtx[row, orgCol] >= 0)
                    candidateNotes.Add((orgCol, orgMtx[row, orgCol]));
            }

            if (candidateNotes.Count == 0)
                return false;

            int targetCol = availableCols[random.Next(availableCols.Count)];
            (int orgCol, int noteIndex) selectedNote = candidateNotes[random.Next(candidateNotes.Count)];

            if (isHoldNoteTailTooClose(newMatrix, orgMtx, timeAxis, row, selectedNote.orgCol, targetCol, beatLength))
                return false;

            newMatrix[row, targetCol] = selectedNote.noteIndex;

            return true;
        }

        private static bool isHoldNoteTailTooClose(NoteMatrix newMatrix,
                                                   NoteMatrix orgMtx,
                                                   Span<int> timeAxis,
                                                   int row,
                                                   int orgCol,
                                                   int targetCol,
                                                   double beatLength)
        {
            double minTimeDistance = (beatLength / 2.5) - 10;

            int rows = orgMtx.Rows;
            int holdLength = 0;

            for (int r = row + 1; r < rows; r++)
            {
                if (orgMtx[r, orgCol] == NoteMatrix.HOLD_BODY)
                    holdLength++;
                else
                    break;
            }

            bool isHoldNote = holdLength > 0;

            if (!isHoldNote || holdLength == 0)
                return false;

            int tailRow = row + holdLength;

            if (tailRow < timeAxis.Length && tailRow < newMatrix.Rows)
            {
                for (int r = row + 1; r <= tailRow; r++)
                {
                    if (r < newMatrix.Rows && newMatrix[r, targetCol] >= 0)
                    {
                        double timeDistance = timeAxis[r] - timeAxis[row + holdLength];
                        if (timeDistance < minTimeDistance) return true;

                        break;
                    }
                }
            }

            return false;
        }

        private static void tryClearSpaceAndInsert(NoteMatrix orgMtx, NoteMatrix newMatrix, Span<int> timeAxis, int emptyRow, int targetCols, int originalCols, double beatLength, Random random)
        {
            double timeThreshold = (beatLength / 14) + 10;
            var processedCols = new HashSet<int>();

            var timeRangeRows = new List<int>();

            for (int row = 0; row < newMatrix.Rows; row++)
            {
                if (Math.Abs(timeAxis[row] - timeAxis[emptyRow]) <= timeThreshold)
                    timeRangeRows.Add(row);
            }

            if (timeRangeRows.Count == 0)
                return;

            List<int> colsToTry = Enumerable.Range(0, targetCols).ToList();
            shuffleList(colsToTry, random);

            foreach (int col in colsToTry)
            {
                if (processedCols.Contains(col)) continue;

                bool hasNotesToRemove = false;

                foreach (int row in timeRangeRows)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        hasNotesToRemove = true;
                        break;
                    }
                }

                if (!hasNotesToRemove)
                    continue;

                var originalValues = new Dictionary<int, int>();
                foreach (int row in timeRangeRows) originalValues[row] = newMatrix[row, col];

                foreach (int row in timeRangeRows)
                {
                    if (newMatrix[row, col] >= 0)
                        newMatrix[row, col] = NoteMatrix.EMPTY;
                }

                bool createsEmptyRows = false;

                foreach (int row in timeRangeRows)
                {
                    bool isEmptyRow = true;

                    for (int c = 0; c < targetCols; c++)
                    {
                        if (newMatrix[row, c] != NoteMatrix.EMPTY)
                        {
                            isEmptyRow = false;
                            break;
                        }
                    }

                    if (isEmptyRow)
                    {
                        createsEmptyRows = true;
                        break;
                    }
                }

                if (createsEmptyRows)
                {
                    foreach (KeyValuePair<int, int> kvp in originalValues) newMatrix[kvp.Key, col] = kvp.Value;
                    processedCols.Add(col);
                    continue;
                }

                if (tryInsertNoteDirectly(newMatrix, orgMtx, timeAxis, emptyRow, targetCols, originalCols, beatLength, random))
                    return;

                foreach (KeyValuePair<int, int> kvp in originalValues) newMatrix[kvp.Key, col] = kvp.Value;
                processedCols.Add(col);
            }
        }

        private static bool isPositionAvailableForEmptyRow(NoteMatrix matrix,
                                                           Span<int> timeAxis,
                                                           int row,
                                                           int col,
                                                           double beatLength)
        {
            if (matrix[row, col] != NoteMatrix.EMPTY)
                return false;

            int rows = matrix.Rows;

            for (int r = Math.Max(0, row - 3); r < row; r++)
            {
                if (timeAxis[row] - timeAxis[r] <= (beatLength / 2.5) + 10)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == NoteMatrix.HOLD_BODY)
                        return false;
                }
            }

            for (int r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
            {
                if (timeAxis[r] - timeAxis[row] <= (beatLength / 2.5) + 10)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == NoteMatrix.HOLD_BODY)
                        return false;
                }
            }

            return true;
        }

        private static void shuffleList<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private class OscillatorGenerator
        {
            private readonly int maxValue;
            private int currentValue;
            private int direction;
            private readonly bool isSpecialCase;

            public OscillatorGenerator(int maxValue, Random? random = null)
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
}
