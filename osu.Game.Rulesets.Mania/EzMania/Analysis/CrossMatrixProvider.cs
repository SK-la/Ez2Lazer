// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    /// <summary>
    ///     交叉矩阵提供者，用于SR计算中的列间 权重矩阵
    /// </summary>
    public static class CrossMatrixProvider
    {
        /// <summary>
        ///     最大支持的键数（与 <see cref="default_cross_matrices"/> 最大索引一致）。
        /// </summary>
        public const int MAX_SUPPORTED_KEY_COUNT = 18;

        /// <summary>
        ///     默认交叉矩阵数据，表示各键位两侧的权重分布
        ///     索引0对应CS=0占位，索引k对应K=k
        /// </summary>
        // 玩家自定义矩阵见 SetCustomMatrix / ValidateMatrix；未来可在游戏内配置 UI。
        private static readonly double[][] default_cross_matrices =
        [
            [-1], // CS=0 placeholder (never requested via GetMatrix)
            [0.075, 0.075],
            [0.125, 0.05, 0.125],
            [0.125, 0.125, 0.125, 0.125],
            [0.175, 0.25, 0.05, 0.25, 0.175],
            [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
            [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
            [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
            [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
            [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
            [0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325], // 10key
            [0.5625, 0.675, 0.625, 0.475, 0.325, 0.125, 0.15, 0.275, 0.425, 0.575, 0.5625, 0.5625], // 11key (from K=10 & K=12)
            [0.8, 0.8, 0.8, 0.6, 0.4, 0.2, 0.05, 0.2, 0.4, 0.6, 0.8, 0.8, 0.8], // 12key
            [0.6, 0.6, 0.5, 0.4, 0.35, 0.25, 0.075, 0.15, 0.35, 0.45, 0.5, 0.5, 0.6, 0.6], // 13key (from K=12 & K=14)
            [0.4, 0.4, 0.2, 0.2, 0.3, 0.3, 0.1, 0.1, 0.3, 0.3, 0.2, 0.2, 0.4, 0.4, 0.4], // 14key
            [0.4, 0.4, 0.2, 0.2, 0.35, 0.35, 0.15, 0.1, 0.2, 0.25, 0.3, 0.3, 0.3, 0.3, 0.4, 0.4], // 15key (from K=14 & K=16)
            [0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.2, 0.1, 0.1, 0.2, 0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.4], // 16key
            [0.4, 0.4, 0.2, 0.3, 0.3, 0.4, 0.2, 0.2, 0.1, 0.15, 0.35, 0.3, 0.3, 0.2, 0.4, 0.3, 0.4, 0.4], // 17key (from K=16 & K=18)
            [0.4, 0.4, 0.2, 0.4, 0.2, 0.4, 0.2, 0.3, 0.1, 0.1, 0.3, 0.2, 0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4] // 18key
        ];

        private static readonly Dictionary<int, double[]> custom_matrices = new Dictionary<int, double[]>();

        private static readonly Dictionary<int, double[]> inferred_matrices_cache = new Dictionary<int, double[]>();

        /// <summary>
        ///     由相邻偶数键同位置系数推断奇数键交叉矩阵（长度 k+1）。
        /// </summary>
        internal static double[] InferMatrixFromNeighbors(int k)
        {
            double[]? lower = getDefaultMatrix(k - 1);
            double[]? upper = getDefaultMatrix(k + 1);

            if (lower == null || upper == null || !isMatrixValid(lower, k - 1) || !isMatrixValid(upper, k + 1))
                throw new InvalidOperationException($"Cannot infer cross matrix for {k}k: neighbour matrices are missing or invalid.");

            double[] result = new double[k + 1];

            for (int i = 0; i <= k; i++)
            {
                int lowerIndex = Math.Min(i, lower.Length - 1);
                int upperIndex = Math.Min(i, upper.Length - 1);
                result[i] = (lower[lowerIndex] + upper[upperIndex]) / 2;
            }

            return result;
        }

        /// <summary>
        ///     设置玩家/调试用的自定义交叉矩阵，覆盖默认表。
        ///     未来可在游戏内由玩家配置；调用方须保证长度与系数合法（见 <see cref="ValidateMatrix"/>）。
        /// </summary>
        /// <param name="k">键数</param>
        /// <param name="matrix">长度为 k+1 的非负系数；null 表示清除该键自定义并回退默认/推断表</param>
        public static void SetCustomMatrix(int k, double[]? matrix)
        {
            if (k < 1 || k > MAX_SUPPORTED_KEY_COUNT)
                throw new ArgumentOutOfRangeException(nameof(k), $"不支持的键数: {k}，支持范围: 1-{MAX_SUPPORTED_KEY_COUNT}");

            if (matrix == null)
            {
                custom_matrices.Remove(k);
                return;
            }

            ValidateMatrix(k, matrix);
            custom_matrices[k] = matrix;
        }

        /// <summary>
        ///     校验交叉矩阵是否符合 SR 计算约定（长度 k+1，系数非负）。
        /// </summary>
        public static void ValidateMatrix(int k, double[] matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (matrix.Length != k + 1)
                throw new ArgumentException($"交叉矩阵长度必须为 {k + 1}（键数 {k}），实际为 {matrix.Length}。", nameof(matrix));

            for (int i = 0; i < matrix.Length; i++)
            {
                if (matrix[i] < 0 || double.IsNaN(matrix[i]) || double.IsInfinity(matrix[i]))
                    throw new ArgumentOutOfRangeException(nameof(matrix), $"交叉矩阵索引 {i} 的值无效: {matrix[i]}。");
            }
        }

        /// <summary>
        ///     清除全部自定义矩阵与推断缓存（仅测试使用）。
        /// </summary>
        internal static void ResetStateForTests()
        {
            custom_matrices.Clear();
            inferred_matrices_cache.Clear();
        }

        /// <summary>
        ///     获取指定键数(K)的交叉矩阵
        /// </summary>
        /// <param name="k">键数</param>
        /// <returns>交叉矩阵数组，如果不支持返回null</returns>
        public static double[]? GetMatrix(int k)
        {
            if (k < 1 || k > MAX_SUPPORTED_KEY_COUNT)
                return null;

            if (custom_matrices.TryGetValue(k, out double[]? customMatrix))
                return customMatrix;

            double[]? matrix = getDefaultMatrix(k);

            if (matrix != null && isMatrixValid(matrix, k))
                return matrix;

            if (k % 2 == 1)
            {
                if (!inferred_matrices_cache.TryGetValue(k, out double[]? cached))
                {
                    cached = InferMatrixFromNeighbors(k);
                    inferred_matrices_cache[k] = cached;
                }

                return cached;
            }

            return null;
        }

        private static double[]? getDefaultMatrix(int k)
        {
            if (k < 1 || k >= default_cross_matrices.Length)
                return null;

            return default_cross_matrices[k];
        }

        private static bool isMatrixValid(double[] matrix, int k)
        {
            if (matrix.Length != k + 1)
                return false;

            foreach (double value in matrix)
            {
                if (value < 0)
                    return false;
            }

            return true;
        }
    }
}
