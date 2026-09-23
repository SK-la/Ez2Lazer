// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 一次性对齐谱面物件 <see cref="ManiaHitWindows"/>（HitMode / 非 O2 窗口 / O2 StartTime BPM 基线）。
    /// 该入口为纯烘焙，不读写 O2 全局运行态，Session / Race 可安全并发调用。
    /// </summary>
    public static class ManiaWindowBaker
    {
        public static void Align(IBeatmap beatmap, IGameplayEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);

            Align(beatmap, environment.ManiaHitMode);
        }

        /// <summary>
        /// 烘焙本体：只依赖 hitmode，live / 仿真 / Race 共用同一份实现。
        /// </summary>
        public static void Align(IBeatmap beatmap, EzEnumHitMode hitMode)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            bool isO2Jam = hitMode == EzEnumHitMode.O2Jam;

            foreach (var hitObject in beatmap.HitObjects)
                alignRecursive(hitObject, beatmap, hitMode, isO2Jam);
        }

        /// <summary>
        /// 本地游玩入口：除纯窗口烘焙外，初始化 HUD / press-time BPM 所需的 O2 运行态。
        /// </summary>
        public static void AlignForLive(IBeatmap beatmap, IGameplayEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);

            if (environment.ManiaHitMode == EzEnumHitMode.O2Jam)
                O2HitModeExtension.InitializeRuntime(beatmap);

            Align(beatmap, environment.ManiaHitMode);
        }

        private static void alignRecursive(HitObject hitObject, IBeatmap beatmap, EzEnumHitMode hitMode, bool isO2Jam)
        {
            if (hitObject.HitWindows is ManiaHitWindows maniaHitWindows)
            {
                maniaHitWindows.SetHitMode(hitMode);

                // O2Jam：按物件 StartTime 写入基线 BPM（auto-miss / note-lock 扫描用）。
                // 用户触发判定走 press-time BPM，不 mutate 物件窗口。
                if (isO2Jam)
                    maniaHitWindows.BPM = O2HitModeExtension.SafeBpm(beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime).BPM);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                alignRecursive(nested, beatmap, hitMode, isO2Jam);
        }
    }
}
