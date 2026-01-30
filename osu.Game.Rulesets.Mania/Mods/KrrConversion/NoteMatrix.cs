using System;
using System.Runtime.InteropServices;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    internal class NoteMatrix
    {
        private readonly int[,] data;

        public const int EMPTY = -1;
        public const int HOLD_BODY = -7;

        public int Rows => data.GetLength(0);
        public int Cols => data.GetLength(1);

        public NoteMatrix(int rows, int cols)
        {
            data = new int[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                    data[i, j] = EMPTY;
            }
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

        public Span<int> AsSpan()
            => MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);

        public Span<int> GetRowSpan(int row)
        {
            if (row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException($"Index out of range: row={row}");

            return MemoryMarshal.CreateSpan(ref data[row, 0], Cols);
        }

        public NoteMatrix Clone()
        {
            var clone = new NoteMatrix(Rows, Cols);
            Array.Copy(data, clone.data, data.Length);
            return clone;
        }

        public void SwapColumns(int colA, int colB)
        {
            if (colA == colB) return;

            if (colA < 0 || colA >= Cols || colB < 0 || colB >= Cols)
                throw new ArgumentOutOfRangeException($"Index out of range: colA={colA}, colB={colB}");

            for (int row = 0; row < Rows; row++)
            {
                int temp = data[row, colA];
                data[row, colA] = data[row, colB];
                data[row, colB] = temp;
            }
        }
    }

    internal class BoolMatrix
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

        public Span<bool> AsSpan()
            => MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);
    }

    internal class DoubleMatrix
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

        public Span<double> AsSpan()
            => MemoryMarshal.CreateSpan(ref data[0, 0], data.Length);
    }
}
