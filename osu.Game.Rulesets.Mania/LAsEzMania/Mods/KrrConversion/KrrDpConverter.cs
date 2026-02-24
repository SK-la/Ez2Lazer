using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public static class KrrDpConverter
    {
        private const int random_seed = 114514;

        public static void Transform(ManiaBeatmap beatmap, KrrDpOptions options)
        {
            int originalKeys = (int)beatmap.Difficulty.CircleSize;
            int targetKeys = options.ModifyKeys ?? originalKeys;

            bool useStartOnly = options.ModifyKeys.HasValue && targetKeys - originalKeys >= 0;
            bool expandHoldBody = !useStartOnly;

            List<ManiaHitObject> notes = beatmap.HitObjects.ToList();

            (NoteMatrix matrix, List<int> timeAxisTemp) = KrrN2NcConverter.BuildMatrix(beatmap, notes, originalKeys, expandHoldBody);
            NoteMatrix processedMatrix = processMatrix(matrix, timeAxisTemp, beatmap, notes, originalKeys, options);

            applyChangesToHitObjects(beatmap, notes, processedMatrix);
        }

        private static NoteMatrix processMatrix(NoteMatrix matrix, List<int> timeAxis, ManiaBeatmap beatmap, List<ManiaHitObject> notes, int originalKeys, KrrDpOptions options)
        {
            int cs = matrix.Cols;
            int targetKeys = cs;
            bool lMirrorFlag = options.LMirror;
            bool rMirrorFlag = options.RMirror;

            double bpm = beatmap.BeatmapInfo.BPM;

            if (bpm <= 0)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(0).BeatLength;
                bpm = beatLength > 0 ? 60000.0 / beatLength : 180;
            }

            double convertTime = (60000 / bpm) * 2 + 10;

            NoteMatrix lMtx = matrix.Clone();
            NoteMatrix rMtx = matrix.Clone();

            if (lMirrorFlag) mirrorMtx(lMtx);
            if (rMirrorFlag) mirrorMtx(rMtx);

            bool useN2Nc = false;
            int lMax = cs;
            int lMin = cs;
            int rMax = cs;
            int rMin = cs;

            if (options.ModifyKeys.HasValue)
            {
                targetKeys = options.ModifyKeys.Value;
                useN2Nc = true;
            }

            if (options.LDensity)
            {
                lMax = options.LMaxKeys;
                lMin = options.LMinKeys;
                useN2Nc = true;
            }

            if (options.RDensity)
            {
                rMax = options.RMaxKeys;
                rMin = options.RMinKeys;
                useN2Nc = true;
            }

            if (options.LRemove)
                lMtx = new NoteMatrix(matrix.Rows, targetKeys);

            if (options.RRemove)
                rMtx = new NoteMatrix(matrix.Rows, targetKeys);

            if (useN2Nc)
            {
                var rng = new Random(random_seed);
                Span<int> timeAxisSpan = CollectionsMarshal.AsSpan(timeAxis);
                Span<double> beatLengthAxis = KrrN2NcConverter.GenerateBeatLengthAxis(timeAxisSpan, notes, beatmap);
                Span<int> endTimeIndexAxis = KrrN2NcConverter.GenerateEndTimeIndex(notes);
                Span<int> orgColIndex = KrrN2NcConverter.GenerateOrgColIndex(matrix);

                if (!options.LRemove)
                    lMtx = KrrN2NcConverter.DoKeys(lMtx, endTimeIndexAxis, timeAxisSpan, beatLengthAxis, orgColIndex, cs, targetKeys, lMax, lMin, convertTime, rng);

                if (!options.RRemove)
                    rMtx = KrrN2NcConverter.DoKeys(rMtx, endTimeIndexAxis, timeAxisSpan, beatLengthAxis, orgColIndex, cs, targetKeys, rMax, rMin, convertTime, rng);
            }

            return concatenateHorizontal(lMtx, rMtx);
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
                    newObjects.Add(KrrConversionHelper.CloneWithColumn(notes[oldIndex], col));
            }

            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column));
        }

        private static void mirrorMtx(NoteMatrix matrix)
        {
            int colCount = matrix.Cols;
            int midPoint = colCount / 2;

            for (int i = 0; i < midPoint; i++)
            {
                int mirrorIndex = colCount - 1 - i;
                matrix.SwapColumns(i, mirrorIndex);
            }
        }

        private static NoteMatrix concatenateHorizontal(NoteMatrix matrix1, NoteMatrix matrix2)
        {
            if (matrix1.Rows != matrix2.Rows)
                throw new ArgumentException("矩阵行数必须相同才能进行横向拼接");

            var result = new NoteMatrix(matrix1.Rows, matrix1.Cols + matrix2.Cols);

            Span<int> matrix1Span = matrix1.AsSpan();
            Span<int> matrix2Span = matrix2.AsSpan();
            Span<int> resultSpan = result.AsSpan();

            for (int i = 0; i < matrix1.Rows; i++)
            {
                int matrix1RowOffset = i * matrix1.Cols;
                int matrix2RowOffset = i * matrix2.Cols;
                int resultRowOffset = i * result.Cols;

                for (int j = 0; j < matrix1.Cols; j++)
                    resultSpan[resultRowOffset + j] = matrix1Span[matrix1RowOffset + j];

                for (int j = 0; j < matrix2.Cols; j++)
                    resultSpan[resultRowOffset + matrix1.Cols + j] = matrix2Span[matrix2RowOffset + j];
            }

            return result;
        }
    }

    public class KrrDpOptions
    {
        public int? ModifyKeys { get; set; }
        public bool LMirror { get; set; }
        public bool LDensity { get; set; }
        public bool LRemove { get; set; }
        public int LMaxKeys { get; set; }
        public int LMinKeys { get; set; }
        public bool RMirror { get; set; }
        public bool RDensity { get; set; }
        public bool RRemove { get; set; }
        public int RMaxKeys { get; set; }
        public int RMinKeys { get; set; }
    }
}
