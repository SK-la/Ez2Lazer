// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.StableClassic
{
    /// <summary>
    /// osu!standard 的 StableClassic 判定总入口：判定档是否生效，以及把 classic 语义绑到谱面。
    /// </summary>
    /// <remarks>
    /// 判定知识按面分布在其实现类型上，本类只做「是否生效」与「怎么绑定」：
    /// <list type="bullet">
    /// <item>判定窗：<see cref="StableStdHitWindows"/>。</item>
    /// <item>计分（档位分值 / 连击 / 准确率 / 总分）：<see cref="StableStdScoring"/>。</item>
    /// <item>滑条评分与跟踪门：<see cref="StableClassicSliderRules"/>。</item>
    /// <item>note lock：<see cref="StableClassicNoteLock"/>。</item>
    /// <item>输入跟踪的帧循环：<c>Objects/Drawables/SliderInputManager</c>（上游文件，按 <see cref="Slider.ClassicSliderBehaviour"/> 分叉）。</item>
    /// <item>局内计分挂钩：<c>Scoring/OsuScoreProcessor</c>。</item>
    /// </list>
    /// 上游字段 <see cref="Slider.ClassicSliderBehaviour"/> 同时被经典 mod（<c>OsuModClassic</c>）写入，
    /// 因此字段留在上游，本目录只解释它的语义。经典 mod 是独立开关，不是本档的前置条件：
    /// 本档生效时，即使没挂经典 mod 或经典 mod 关掉了对应选项，判定窗、滑条、note lock、计分也全部按 stable 口径运行。
    /// </remarks>
    public static class OsuStableClassic
    {
        /// <summary>判定档是否为本档。判定档解析的唯一入口，热路径以外的地方都改调这里。</summary>
        public static bool IsActive(EzEnumJudgeMode judgeMode) => judgeMode == EzEnumJudgeMode.StableClassic;

        /// <summary>
        /// 就地绑定 classic 判定语义：替换物件判定窗，滑条切到 classic 判定，并丢弃按旧语义缓存的判定对象。
        /// 必须在全谱模拟（<c>ScoreProcessor.SimulateAutoplay</c>）之前完成，且只能作用于调用方独占的谱面实例。
        /// </summary>
        public static void BindSemantics(IBeatmap beatmap)
        {
            double od = beatmap.Difficulty.OverallDifficulty;

            foreach (var hitObject in beatmap.HitObjects)
                applyRecursive(hitObject, od);
        }

        private static void applyRecursive(HitObject hitObject, double overallDifficulty)
        {
            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
            {
                HitWindows classic = new StableStdHitWindows();
                classic.SetDifficulty(overallDifficulty);
                hitObject.HitWindows = classic;
            }

            if (hitObject is Slider slider)
                slider.ClassicSliderBehaviour = true;

            // 判定对象缓存了按 classic 分支创建的结果（滑条主体、滑条头、滑条尾的档位都取决于
            // ClassicSliderBehaviour）。转换期间可能已被创建过，这里必须丢弃，否则主体会被当成 Ignore 判档，
            // 既不进准确率分母也不进最大分。
            hitObject.ResetJudgement();

            foreach (var nested in hitObject.NestedHitObjects)
                applyRecursive(nested, overallDifficulty);
        }

        /// <summary>
        /// 把 stable 判定所需的 Drawable 侧规则装到本局：classic note lock 与滑条头遮挡。
        /// </summary>
        /// <remarks>
        /// 只由判定档触发，与经典 mod 无关；经典 mod 的同一套设置随后应用也不会改变结果。
        /// 只覆盖判定相关的输入拦截；血量、淡出、尾音等表现仍由各自设置与 mod 决定。
        /// </remarks>
        public static void ApplyLiveRules(OsuPlayfield playfield, bool autopilot)
        {
            ArgumentNullException.ThrowIfNull(playfield);

            playfield.HitPolicy = new LegacyHitPolicy(StableClassicNoteLock.HittableRange(autopilot));
            playfield.BlockInputUnderSliderHeads = true;
        }
    }
}
