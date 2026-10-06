// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion
{
    public static class KrrN2NcConverter
    {
        internal static (NoteMatrix matrix, List<int> timeAxis) BuildMatrix(ManiaBeatmap beatmap, List<ManiaHitObject> notes, int cols, bool expandHoldBody) =>
            buildMatrix(beatmap, notes, cols, expandHoldBody);

        internal static Span<double> GenerateBeatLengthAxis(Span<int> timeAxis, List<ManiaHitObject> maniaObjects, ManiaBeatmap beatmap) => generateBeatLengthAxis(timeAxis, maniaObjects, beatmap);

        internal static Span<int> GenerateEndTimeIndex(List<ManiaHitObject> maniaObjects) => generateEndTimeIndex(maniaObjects);

        internal static Span<int> GenerateOrgColIndex(NoteMatrix matrix) => generateOrgColIndex(matrix);

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
                                          Random random) => doKeys(matrix, endTimeIndexAxis, timeAxis, beatlengthAxis, orgColIndex, originalKeys, targetKeys, maxKeys, minKeys, convertTime, random);

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

            // 直接引用 beatmap.HitObjects：矩阵里的索引都指向它，而重建只在全部读取完成后发生，
            // 所以不需要一份 ToList 快照（原实现每次转换都要复制整份列表）。
            List<ManiaHitObject> notes = beatmap.HitObjects;

            bool expandHoldBody = targetKeys - originalKeys < 0;
            (NoteMatrix matrix, List<int> timeAxisTemp) = buildMatrix(beatmap, notes, originalKeys, expandHoldBody);

            // 空谱面（0 行）没有 note 可转换：下游的键数扩展路径只为「有 note」的输入设计（convertMtx 对 0 行直接抛异常），
            // 这里直接结束——「空谱面 + 改键数」应该是空结果，而不是一次转换失败。
            if (matrix.Rows == 0)
                return;

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
            var newObjects = new List<ManiaHitObject>(notes.Count);
            int targetKeys = processedMatrix.Cols;
            Span<int> matrixSpan = processedMatrix.AsSpan();

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int oldIndex = matrixSpan[i];
                int col = i % targetKeys;

                if (oldIndex >= 0 && oldIndex < notes.Count)
                    newObjects.Add(KrrConversionHelper.CloneWithColumn(notes[oldIndex], col));
            }

            // 矩阵按行（时间升序）扫描、行内按列升序，落键本就是 (StartTime, Column) 升序；
            // 只有确实乱序时才付一次排序代价，避免每次转换都走 OrderBy().ThenBy() 的三段缓冲。
            IEnumerable<ManiaHitObject> ordered = isOrderedByStartTimeThenColumn(newObjects)
                ? newObjects
                : newObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column);

            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(ordered);
        }

        private static bool isOrderedByStartTimeThenColumn(List<ManiaHitObject> notes)
        {
            for (int i = 1; i < notes.Count; i++)
            {
                ManiaHitObject previous = notes[i - 1];
                ManiaHitObject current = notes[i];

                if (current.StartTime < previous.StartTime)
                    return false;

                if (current.StartTime == previous.StartTime && current.Column < previous.Column)
                    return false;
            }

            return true;
        }

        private static bool isOrderedByStartTime(List<ManiaHitObject> notes)
        {
            for (int i = 1; i < notes.Count; i++)
            {
                if (notes[i].StartTime < notes[i - 1].StartTime)
                    return false;
            }

            return true;
        }

        private static (NoteMatrix, List<int>) buildMatrix(ManiaBeatmap beatmap, List<ManiaHitObject> notes, int cols, bool expandHoldBody)
        {
            // getMTXandTimeAxis(): unique start times only, ordered
            var seenStartTimes = new HashSet<int>();
            var uniqueStartTimes = new List<int>(notes.Count);

            foreach (var note in notes)
            {
                if (seenStartTimes.Add(toTimeKey(note.StartTime)))
                    uniqueStartTimes.Add(toTimeKey(note.StartTime));
            }

            uniqueStartTimes.Sort();

            int timeCount = uniqueStartTimes.Count;
            var matrix = new NoteMatrix(timeCount, cols);

            // timeKey → 行号：原实现对每个 note 做一次 IndexOf，整段是 O(notes × rows)。
            var timeIndexByKey = new Dictionary<int, int>(timeCount);

            for (int i = 0; i < timeCount; i++)
                timeIndexByKey[uniqueStartTimes[i]] = i;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                int colIndex = Math.Clamp(note.Column, 0, cols - 1);

                // 时间轴就是所有起始时刻的去重集合，所以这里必定命中。
                matrix[timeIndexByKey[toTimeKey(note.StartTime)], colIndex] = i;
            }

            if (!expandHoldBody)
                return (matrix, uniqueStartTimes);

            return expandHoldBodyMatrix(matrix, uniqueStartTimes, notes, cols);
        }

        private static (NoteMatrix, List<int>) expandHoldBodyMatrix(NoteMatrix matrix, List<int> timeAxis, List<ManiaHitObject> notes, int cols)
        {
            var allTimes = new HashSet<int>(timeAxis);

            foreach (var note in notes)
                allTimes.Add(toTimeKey(note is HoldNote hold ? hold.EndTime : note.StartTime));

            var newTimeAxis = new List<int>(allTimes);
            newTimeAxis.Sort();

            var newTimeIndexByKey = new Dictionary<int, int>(newTimeAxis.Count);

            for (int i = 0; i < newTimeAxis.Count; i++)
                newTimeIndexByKey[newTimeAxis[i]] = i;

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
                    // 新时间轴是所有起始时刻与长条结束时刻的并集，头尾都必定在其中。
                    int startIndex = newTimeIndexByKey[toTimeKey(hold.StartTime)];
                    int endIndex = newTimeIndexByKey[toTimeKey(hold.EndTime)];
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

        private static int toTimeKey(double time) => (int)Math.Round(time);

        private static Span<double> generateBeatLengthAxis(Span<int> timeAxis, List<ManiaHitObject> maniaObjects, ManiaBeatmap beatmap)
        {
            double[] result = new double[timeAxis.Length];
            result.AsSpan().Fill(-1.0);

            // 时间轴是 note 起始时刻的升序去重，所以按 StartTime 升序单趟推进即可对齐；
            // 原实现为此建了匿名类型再 OrderBy 一趟（排序结果只用来取值，索引字段根本没用上）。
            IEnumerable<ManiaHitObject> ordered = isOrderedByStartTime(maniaObjects) ? maniaObjects : maniaObjects.OrderBy(n => n.StartTime);

            int currentTimeAxisIndex = 0;

            foreach (var note in ordered)
            {
                int startTime = toTimeKey(note.StartTime);

                while (currentTimeAxisIndex < timeAxis.Length &&
                       timeAxis[currentTimeAxisIndex] < startTime)
                    currentTimeAxisIndex++;

                if (currentTimeAxisIndex < timeAxis.Length && timeAxis[currentTimeAxisIndex] == startTime)
                    result[currentTimeAxisIndex] = beatmap.ControlPointInfo.TimingPointAt(note.StartTime).BeatLength;
            }

            return result;
        }

        private static Span<int> generateEndTimeIndex(List<ManiaHitObject> maniaObjects)
        {
            int[] result = new int[maniaObjects.Count];

            for (int i = 0; i < maniaObjects.Count; i++)
            {
                ManiaHitObject item = maniaObjects[i];
                result[i] = toTimeKey(item is HoldNote h ? h.EndTime : item.StartTime);
            }

            return result;
        }

        private static Span<int> generateOrgColIndex(NoteMatrix matrix)
        {
            int cols = matrix.Cols;
            Span<int> matrixSpan = matrix.AsSpan();

            int maxIndex = -1;

            for (int i = 0; i < matrixSpan.Length; i++)
                maxIndex = Math.Max(maxIndex, matrixSpan[i]);

            if (maxIndex < 0)
                return Span<int>.Empty;

            int[] orgColIndex = new int[maxIndex + 1];
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
                    var oldIndex = new KrrOscillator(originalKeys - 1, random);
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
                var insertIndex = new KrrOscillator(originalKeys + col, random);
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
                Logger.Log($"[KrrN2NcConverter] Convert failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                throw;
            }
        }

        private static NoteMatrix performInitialConvert(NoteMatrix matrix, NoteMatrix oldMtx, NoteMatrix insertMtx, int targetKeys, int turn, int rows, int originalCols, bool ifMaxKeysEqual)
        {
            var newMatrix = new NoteMatrix(rows, targetKeys);

            // 原实现每行都 new int[targetKeys]：缓冲搬到循环外，每行显式重置成 EMPTY 即可。
            var tempRow = new int[targetKeys];

            for (int i = 0; i < rows; i++)
            {
                tempRow.AsSpan().Fill(NoteMatrix.EMPTY);

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

            // 三行小缓冲只在本次调用内使用：小键数走栈上分配，不再每次转换都产生三个堆数组。
            // 长度为异常大（列数来自谱面，不保证在 mod 设置的 18 以内）时回退堆数组：栈只有 1MB，不能按谱面数据定长。
            const int stack_buffer_limit = 64;

            if (targetKeys <= stack_buffer_limit)
            {
                Span<int> endTimeTempRow = stackalloc int[stack_buffer_limit];
                Span<int> convertTimePointRow = stackalloc int[stack_buffer_limit];
                Span<int> orgColIndexRow = stackalloc int[stack_buffer_limit];

                fillDeleteMark(newMatrixSpan, markSpan, timeAxis, endTimeIndexAxis, beatLengthAxis, orgColIndexAxis, targetKeys,
                               endTimeTempRow, convertTimePointRow, orgColIndexRow);
            }
            else
            {
                int[] heapBuffers = new int[targetKeys * 3];

                fillDeleteMark(newMatrixSpan, markSpan, timeAxis, endTimeIndexAxis, beatLengthAxis, orgColIndexAxis, targetKeys,
                               heapBuffers.AsSpan(0, targetKeys),
                               heapBuffers.AsSpan(targetKeys, targetKeys),
                               heapBuffers.AsSpan(targetKeys * 2, targetKeys));
            }

            return mark;
        }

        private static void fillDeleteMark(Span<int> newMatrixSpan,
                                           Span<bool> markSpan,
                                           Span<int> timeAxis,
                                           Span<int> endTimeIndexAxis,
                                           Span<double> beatLengthAxis,
                                           Span<int> orgColIndexAxis,
                                           int targetKeys,
                                           Span<int> endTimeTempRow,
                                           Span<int> convertTimePointRow,
                                           Span<int> orgColIndexRow)
        {
            // 栈路径给的缓冲可能比 targetKeys 长：只用前 targetKeys 项，语义与堆路径完全一致。
            endTimeTempRow = endTimeTempRow[..targetKeys];
            convertTimePointRow = convertTimePointRow[..targetKeys];
            orgColIndexRow = orgColIndexRow[..targetKeys];

            endTimeTempRow.Clear();
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

            // 原实现每行都 new 两个 List、每轮淘汰都 new 一个 double[]：缓冲搬到循环外，逐次 Clear 复用。
            var activeNotes = new List<int>(cols);
            var candidates = new List<int>(cols);
            double[] weights = new double[cols];

            for (int i = 0; i < rows; i++)
            {
                activeNotes.Clear();

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

                candidates.Clear();
                candidates.AddRange(activeNotes);

                for (int r = 0; r < toRemove && candidates.Count > 0; r++)
                {
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
            // 原实现每轮调整都 new 一个候选表：整个循环只有一个候选表被用到，搬到循环外复用。
            var candidates = new List<int>();

            for (int i = 0; i < columnsToRemove.Count; i++)
            {
                if (random.NextDouble() < 0.25)
                {
                    int currentCol = columnsToRemove[i];
                    int currentWeight = columnWeights[currentCol];

                    candidates.Clear();

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
            // 每个空行都要两个候选列表：搬到行循环外复用，原实现是每行各 new 两个 List。
            var candidateNotes = new List<int>();
            var availablePositions = new List<int>();

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
                    candidateNotes.Clear();
                    int originalCols = orgMtx.Cols;

                    for (int col = 0; col < originalCols; col++)
                    {
                        if (orgMtx[row, col] >= 0)
                            candidateNotes.Add(col);
                    }

                    if (candidateNotes.Count > 0)
                    {
                        int selectedOrgCol = candidateNotes[random.Next(candidateNotes.Count)];

                        availablePositions.Clear();

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

            columnList.Sort(columnComparer);

            var result = new List<int>(Math.Min(colsToRemove, columnList.Count));

            for (int i = 0; i < colsToRemove && i < columnList.Count; i++)
                result.Add(columnList[i].index);

            return result;
        }

        /// <summary>
        /// 与原来的 <c>List.Sort(comparison)</c> 语义一致（权重优先，其次风险），只是把比较委托从每次调用改成静态字段。
        /// </summary>
        private static readonly Comparison<(int index, int weight, double risk)> columnComparer = (a, b) =>
        {
            int weightComparison = a.weight.CompareTo(b.weight);

            if (weightComparison != 0)
                return weightComparison;

            return a.risk.CompareTo(b.risk);
        };

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

        /// <summary>
        /// 空档补齐阶段（<see cref="processEmptyRows"/> 与它调用的两个方法）复用的缓冲。
        /// </summary>
        /// <remarks>
        /// 这些容器原本都建在按行、按列的循环内部，一张长谱面能翻出成千上万个小对象；复用只是把「每次 new」
        /// 换成「每次清空」，写入顺序与 <see cref="Random"/> 的调用次序都保持不变。
        /// </remarks>
        private sealed class EmptyRowBuffers
        {
            public readonly List<int> AvailableCols = new List<int>();
            public readonly List<int> CandidateCols = new List<int>();
            public readonly List<int> CandidateValues = new List<int>();
            public readonly List<int> ColumnsToTry = new List<int>();
            public readonly HashSet<int> ProcessedCols = new HashSet<int>();
            public readonly Dictionary<int, int> ValuesByRow = new Dictionary<int, int>();
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

            var buffers = new EmptyRowBuffers();

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
                    if (tryInsertNoteDirectly(newMatrix, orgMtx, timeAxis, row, targetCols, originalCols, beatLengthAxis[row], random, buffers))
                        continue;

                    tryClearSpaceAndInsert(orgMtx, newMatrix, timeAxis, row, targetCols, originalCols, beatLengthAxis[row], random, buffers);
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
                                                  Random random,
                                                  EmptyRowBuffers buffers)
        {
            List<int> availableCols = buffers.AvailableCols;
            List<int> candidateCols = buffers.CandidateCols;
            List<int> candidateValues = buffers.CandidateValues;

            availableCols.Clear();
            candidateCols.Clear();
            candidateValues.Clear();

            for (int col = 0; col < targetCols; col++)
            {
                if (isPositionAvailableForEmptyRow(newMatrix, timeAxis, row, col, beatLength))
                    availableCols.Add(col);
            }

            if (availableCols.Count == 0)
                return false;

            for (int orgCol = 0; orgCol < originalCols; orgCol++)
            {
                int noteIndex = orgMtx[row, orgCol];

                if (noteIndex >= 0)
                {
                    candidateCols.Add(orgCol);
                    candidateValues.Add(noteIndex);
                }
            }

            if (candidateCols.Count == 0)
                return false;

            int targetCol = availableCols[random.Next(availableCols.Count)];
            int candidate = random.Next(candidateCols.Count);
            int selectedOrgCol = candidateCols[candidate];
            int selectedNoteIndex = candidateValues[candidate];

            if (isHoldNoteTailTooClose(newMatrix, orgMtx, timeAxis, row, selectedOrgCol, targetCol, beatLength))
                return false;

            newMatrix[row, targetCol] = selectedNoteIndex;

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

        private static void tryClearSpaceAndInsert(NoteMatrix orgMtx,
                                                   NoteMatrix newMatrix,
                                                   Span<int> timeAxis,
                                                   int emptyRow,
                                                   int targetCols,
                                                   int originalCols,
                                                   double beatLength,
                                                   Random random,
                                                   EmptyRowBuffers buffers)
        {
            double timeThreshold = (beatLength / 14) + 10;

            HashSet<int> processedCols = buffers.ProcessedCols;
            processedCols.Clear();

            // 时间轴升序去重，所以「与 emptyRow 相距不超过阈值」的行必是连续区间：
            // 原实现为此扫全表并另建一个 List，这里直接算出区间两端。emptyRow 自身恒在区间内（阈值恒为正）。
            int firstRow = emptyRow;

            while (firstRow > 0 && timeAxis[emptyRow] - timeAxis[firstRow - 1] <= timeThreshold)
                firstRow--;

            int lastRow = emptyRow;

            while (lastRow + 1 < newMatrix.Rows && timeAxis[lastRow + 1] - timeAxis[emptyRow] <= timeThreshold)
                lastRow++;

            List<int> colsToTry = buffers.ColumnsToTry;
            colsToTry.Clear();

            for (int col = 0; col < targetCols; col++)
                colsToTry.Add(col);

            shuffleList(colsToTry, random);

            Dictionary<int, int> originalValues = buffers.ValuesByRow;

            foreach (int col in colsToTry)
            {
                if (processedCols.Contains(col)) continue;

                bool hasNotesToRemove = false;

                for (int row = firstRow; row <= lastRow; row++)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        hasNotesToRemove = true;
                        break;
                    }
                }

                if (!hasNotesToRemove)
                    continue;

                originalValues.Clear();

                for (int row = firstRow; row <= lastRow; row++)
                {
                    originalValues[row] = newMatrix[row, col];

                    if (newMatrix[row, col] >= 0)
                        newMatrix[row, col] = NoteMatrix.EMPTY;
                }

                bool createsEmptyRows = false;

                for (int row = firstRow; row <= lastRow; row++)
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
                    restoreColumn(newMatrix, originalValues, col);
                    processedCols.Add(col);
                    continue;
                }

                if (tryInsertNoteDirectly(newMatrix, orgMtx, timeAxis, emptyRow, targetCols, originalCols, beatLength, random, buffers))
                    return;

                restoreColumn(newMatrix, originalValues, col);
                processedCols.Add(col);
            }
        }

        private static void restoreColumn(NoteMatrix newMatrix, Dictionary<int, int> originalValues, int col)
        {
            foreach (KeyValuePair<int, int> kvp in originalValues)
                newMatrix[kvp.Key, col] = kvp.Value;
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
    }
}
