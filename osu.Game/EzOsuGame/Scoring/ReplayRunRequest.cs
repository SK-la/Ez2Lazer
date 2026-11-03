// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行请求：由 <see cref="ReplayRunPurpose"/> 驱动环境解析。
    /// </summary>
    public sealed class ReplayRunRequest
    {
        public Score Score { get; }
        public IBeatmap Beatmap { get; }
        public ReplayRunPurpose Purpose { get; }
        public bool IncludeGlobalManiaOffset { get; init; }

        /// <summary>
        /// 固定判定档位；其余环境字段（offset、frameShift、血量、优先级）仍按 <see cref="Purpose"/> 解析。
        /// </summary>
        /// <remarks>
        /// 只换档位不换整份环境：偏移等字段必须与对照运行一致，否则两次结果无法比较，缓存键也会分叉。
        /// </remarks>
        public EzEnumJudgeMode? JudgeModeOverride { get; init; }

        /// <summary>
        /// 非 null 时完全替代按 <see cref="Purpose"/> 解析的环境（数据迁移用）。
        /// </summary>
        public GameplayEnvironment? EnvironmentOverride { get; init; }

        // TODO(Ez 投影 core)：把「分数 × 判定档位 × offset」的投影收敛成唯一入口。
        //  现状：同一份 replay 已能按不同档位各投影一次（Graph 的 Now 用全局档，V1 用钉死的 StableClassic），
        //        去重发生在 EzReplaySession.sessionRunCache（进程级，键含 purpose/score/beatmap/env）；
        //        消费方只要注入已有的 IEzReplaySession 并发出同一个键，就能共用结果，无需复刻 Graph 的加载机制。
        //        但目前每个消费方仍要自己拼这个请求、自己处理 async 与拖拽 debounce、自己决定无 replay 时如何处置，
        //        而 Graph 里那对 ReplaySessionScore/V1SessionScore 只是 UI 级 memo，不是可复用的载体。
        //  要抽：一个 core/helper，输入 (Score, IBeatmap, ReplayRunPurpose, 档位, offset)，输出 Score / EzScoreTimeline，
        //        内部沿用 sessionRunCache 做去重，并集中定义「无 replay / 无谱面 / 失败」的统一结果，
        //        使 Now、V1 以及后续想拿 stable 结果的 HUD / 结算等组件都走同一条路径。
        //  约束：不要为此新增 DI 注册。载体取「已有的 IEzReplaySession 实例 + 普通静态/扩展 helper」的组合，
        //        需要 DI 的地方继续复用现有解析出来的实例。
        //  一并处理：sessionRunCache 目前无淘汰，投影档位增加会让条目按档位数线性增长，
        //        抽 core 时须同时给出容量上限或清理时机，避免成绩面板反复切换时无界累积。
        //  验收：同一 (score, beatmap, 档位, offset) 在任意组件取到的 Score 逐字段相同，且只实际运行一次。

        public ReplayRunRequest(Score score, IBeatmap beatmap, ReplayRunPurpose purpose)
        {
            Score = score;
            Beatmap = beatmap;
            Purpose = purpose;
        }
    }
}
