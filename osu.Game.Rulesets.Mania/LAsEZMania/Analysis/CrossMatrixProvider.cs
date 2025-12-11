using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.LAsEZMania.Analysis
{
    /// <summary>
    ///     交叉矩阵提供者，用于SR计算中的列间 权重矩阵
    /// </summary>
    public static class CrossMatrixProvider
    {
        /// <summary>
        ///     默认交叉矩阵数据，表示各键位两侧的权重分布
        ///     索引0对应K=1，索引1对应K=2，以此类推
        ///     null表示不支持该键数
        /// </summary>
        private static readonly double[][] default_cross_matrices =
        [
            [-1], // CS=0
            [0.075, 0.075],
            [0.125, 0.05, 0.125],
            [0.125, 0.125, 0.125, 0.125],
            [0.175, 0.25, 0.05, 0.25, 0.175],
            [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
            [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
            [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
            [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
            [0.3, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.3],
            [0.425, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.425], // 10key
            // Inferred matrices for K=11 to 18 based on user-specified patterns
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=11 (odd, unsupported)
            [0.8, 0.8, 0.8, 0.6, 0.4, 0.2, 0.05, 0.2, 0.4, 0.6, 0.8, 0.8, 0.8], // K=12 (even, sides 3 columns higher)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=13 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.2, 0.3, 0.3, 0.1, 0.1, 0.3, 0.3, 0.2, 0.2, 0.4, 0.4, 0.4], // K=14 (wave: low-low-high-high-low-low-high-high)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=15 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.2, 0.1, 0.1, 0.2, 0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.4], // K=16 (wave: low-low-high-high-low-low-high-high-low-low-high-high)
            [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], // K=17 (odd, unsupported)
            [0.4, 0.4, 0.2, 0.4, 0.2, 0.4, 0.2, 0.3, 0.1, 0.1, 0.3, 0.2, 0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4] // K=18 (wave: low-low-high-low-high-low-high-low-low-high-low-high-low-high)
        ];

        /// <summary>
        ///     自定义交叉矩阵，用于覆盖默认数据
        /// </summary>
        private static readonly Dictionary<int, double[]> custom_matrices = new Dictionary<int, double[]>();

        /// <summary>
        ///     设置自定义交叉矩阵
        /// </summary>
        /// <param name="k">键数</param>
        /// <param name="matrix">自定义矩阵数组，如果为null则清除自定义矩阵</param>
        public static void SetCustomMatrix(int k, double[]? matrix)
        {
            if (k < 1 || k > default_cross_matrices.Length)
                throw new ArgumentOutOfRangeException(nameof(k), $"不支持的键数: {k}，支持范围: 1-{default_cross_matrices.Length}");

            if (matrix == null)
                custom_matrices.Remove(k);
            else
                custom_matrices[k] = matrix;
        }

        /// <summary>
        ///     获取指定键数(K)的交叉矩阵
        ///     K表示键数，从1开始索引
        /// </summary>
        /// <param name="k">键数</param>
        /// <returns>交叉矩阵数组，如果不支持返回null</returns>
        public static double[]? GetMatrix(int k)
        {
            if (k < 1 || k > default_cross_matrices.Length)
                return null;

            return custom_matrices.TryGetValue(k, out double[]? customMatrix) ? customMatrix : default_cross_matrices[k];
        }
    }
}
