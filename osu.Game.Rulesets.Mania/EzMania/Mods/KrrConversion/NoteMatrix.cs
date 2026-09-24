// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion
{
    public class NoteMatrix
    {
        private readonly int[,] data;

        public const int EMPTY = -1;
        public const int HOLD_BODY = -7;

        public int Rows => data.GetLength(0);
        public int Cols => data.GetLength(1);

        public NoteMatrix(int rows, int cols)
        {
            data = new int[rows, cols];

            // 数组本身已经是 0，只有 EMPTY(-1) 需要显式填一遍；用扁平 span 单趟填充代替双层索引循环。
            AsSpan().Fill(EMPTY);
        }

        public int this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                return data[row, col];
            }
            set
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                data[row, col] = value;
            }
        }

        // 空矩阵（0 行或 0 列）下 data[0, 0] 越界：返回空 span，让调用方的 Fill / 循环自然成为空操作。
        public Span<int> AsSpan() => data.Length == 0 ? Span<int>.Empty : MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);

        public Span<int> GetRowSpan(int row)
        {
            if (row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException($"Index out of range: row={row}");

            return Cols == 0 ? Span<int>.Empty : MemoryMarshal.CreateSpan(ref data[row, 0], Cols);
        }

        private NoteMatrix(int[,] data)
        {
            this.data = data;
        }

        public NoteMatrix Clone() => new NoteMatrix((int[,])data.Clone());

        public void SwapColumns(int colA, int colB)
        {
            if (colA == colB) return;

            if (colA < 0 || colA >= Cols || colB < 0 || colB >= Cols)
                throw new ArgumentOutOfRangeException($"Index out of range: colA={colA}, colB={colB}");

            for (int row = 0; row < Rows; row++)
            {
                (data[row, colA], data[row, colB]) = (data[row, colB], data[row, colA]);
            }
        }
    }

    public class BoolMatrix
    {
        private readonly bool[,] data;

        public int Rows => data.GetLength(0);
        public int Cols => data.GetLength(1);

        public BoolMatrix(int rows, int cols)
        {
            data = new bool[rows, cols];
        }

        public bool this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                return data[row, col];
            }
            set
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                data[row, col] = value;
            }
        }

        public Span<bool> AsSpan() => data.Length == 0 ? Span<bool>.Empty : MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);
    }

    public class DoubleMatrix
    {
        private readonly double[,] data;

        public int Rows => data.GetLength(0);
        public int Cols => data.GetLength(1);

        public DoubleMatrix(int rows, int cols)
        {
            data = new double[rows, cols];
        }

        public double this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                return data[row, col];
            }
            set
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                    throw new ArgumentOutOfRangeException($"Index out of range: row={row}, col={col}");

                data[row, col] = value;
            }
        }

        public Span<double> AsSpan() => data.Length == 0 ? Span<double>.Empty : MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);
    }
}
