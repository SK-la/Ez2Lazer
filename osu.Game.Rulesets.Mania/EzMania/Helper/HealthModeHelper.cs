// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Rulesets.Mania.EzMania.Helper
{
    /// <summary>
    /// BMS系列数据来自: <see href="https://iidx.org/misc/iidx_lr2_beatoraja_diff">IIDX LR2/Beatoraja 难度表</see>
    /// <para></para>
    /// Ez2AC 数据来自: <see href="https://namu.wiki/w/EZ2AC%20%EC%8B%9C%EB%A6%AC%EC%A6%88/%ED%8C%90%EC%A0%95%EA%B3%BC%20%EC%A0%90%EC%88%98%EC%B2%B4%EA%B3%84">EZ2AC 系列/判决与评分系统</see>
    /// <para></para>
    /// O2Jam 数据来自: <see href="https://en.namu.wiki/w/O2Jam#s-4.2">O2Jam HP Gauge</see>
    /// </summary>
    public static class HealthModeHelper
    {
        public static readonly double[,] HEALTH_MODE_MAP =
        {
            //  305    300    200         50    Miss    Poor
            { 0.004, 0.003, 0.001,   0, -0.010, -0.030, -0.00 }, // Lazer
            // Cool          Good          Bad    Miss
            { 0.003, 0.000, 0.002,   0, -0.010, -0.050, -0.00 }, // O2 Easy
            { 0.002, 0.000, 0.001,   0, -0.007, -0.040, -0.00 }, // O2 Normal
            { 0.001, 0.000, 0.000,   0, -0.005, -0.030, -0.00 }, // O2 Hard
            // Kool   Cool    Good       Miss   Fail
            { 0.004,  0.003,  0.001,  0, -0.03, -0.05, -0.02 }, // Ez2Ac
            // Kool   Cool   Good        Bad    Poor   KPoor
            { 0.0016, 0.0016, 0.0000, 0, -0.05, -0.09, -0.05 }, // IIDX
            { 0.0010, 0.0010, 0.0005, 0, -0.06, -0.10, -0.02 }, // LR2 Hard
            { 0.0015, 0.0012, 0.0003, 0, -0.05, -0.10, -0.05 }, // raja Hard
        };

        public static bool IsBMSHealthMode(EzEnumHealthMode hitMode)
        {
            return hitMode == EzEnumHealthMode.IIDX_HD ||
                   hitMode == EzEnumHealthMode.LR2_HD ||
                   hitMode == EzEnumHealthMode.Raja_HD;
        }
    }
}
