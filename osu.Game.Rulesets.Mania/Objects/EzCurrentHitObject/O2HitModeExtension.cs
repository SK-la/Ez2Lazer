// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    // 代码改编自YuLiangSSS提供的ManiaModO2Judgement
    public static partial class O2HitModeExtension
    {
        public const double COOL = 7500.0;
        public const double GOOD = 22500.0;
        public const double BAD = 31250.0;

        // public const double DEFAULT_BPM = 200;

        // TODO: 💊缺少UI显示，以及合适的开关
        // 是否启用💊, 此处默认开启，否则必须搭配ManiaModO2Judgement.PillMode.Value才能生效
        // 启用 Pill 模式的特殊判定逻辑（如累积/消耗 Pill、使用 CoolCombo 逻辑等）。
        // 注意：初始值和持久化逻辑取决于外部设置/开关，这里仅作为全局运行时状态使用。
        public static bool PillActivated = true; // = ManiaModO2Judgement.PillMode.Value;

        // 💊数量（可绑定）
        // 上限为 5，在达到一定 Cool 连击后会增加，发生较大偏移时会减少。
        public static Bindable<int> PillCount = new Bindable<int>(0);

        // Cool 连击计数（用于追踪在 Cool 判定内的连续命中次数）
        // 语义：每次命中判断在 Cool 范围内时递增；当计数达到 15 时会重置（减去 15）并使 `Pill` 增加（最多至 5）。
        // 若在 Good 范围内则重置为 0；若落入 Bad 范围且拥有 Pill 会消耗 1 个 Pill 并替换判定为 Perfect（见使用处）。
        public static int CoolCombo;

        public static double CoolRange => COOL / NowBeatmapBPM;
        public static double GoodRange => GOOD / NowBeatmapBPM;
        public static double BadRange => BAD / NowBeatmapBPM;
        public static double NowBeatmapBPM;

        /// <summary>
        /// 统一的 Pill 判定逻辑：将原本分散在各 Drawable 的重复实现合并到这里。
        /// 返回值：true 表示继续执行后续判定逻辑；false 表示应中断后续判定（保留以便未来扩展）。
        /// out 参数：
        /// - <paramref name="applyComboBreak"/>：当命中落入 Bad 范围且没有可用 Pill 时为 true。
        /// - <paramref name="upgradeToPerfect"/>：当命中落入 Bad 范围且消耗了 Pill 时为 true（调用者应将该次判定提升为 <see cref="HitResult.Perfect"/>）。
        /// </summary>
        public static bool PillCheck(double timeOffset, out bool applyComboBreak, out bool upgradeToPerfect)
        {
            applyComboBreak = false;
            upgradeToPerfect = false;

            if (!PillActivated)
                return true;

            double offset = Math.Abs(timeOffset);

            if (offset <= CoolRange)
            {
                CoolCombo++;

                if (CoolCombo >= 15)
                {
                    CoolCombo -= 15;

                    if (PillCount.Value < 5)
                        PillCount.Value++;
                }
            }
            else if (offset > CoolRange && offset <= GoodRange)
            {
                CoolCombo = 0;
            }
            else if (offset > GoodRange && offset <= BadRange)
            {
                CoolCombo = 0;

                if (PillCount.Value > 0)
                {
                    // 有 Pill 时：消耗 1 个，并将该次判定提升为 Perfect（不应断连）。
                    PillCount.Value--;
                    upgradeToPerfect = true;
                }
                else
                {
                    // 无 Pill 时：该次判定视作断连（实际表现由调用者决定）。
                    applyComboBreak = true;
                }
            }

            return true;
        }
    }

    public partial class O2DrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                bool applyComboBreak;
                bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak, out upgradeToPerfect);
                if (!cont) return;
            }

            // 此处有潜在的崩溃风险，与播放动画有关，待调查。
            // Replicate base implementation to allow attaching combo semantics overrides.
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                bool applyComboBreak;
                bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak, out upgradeToPerfect);
                if (!cont) return;
            }

            // Replicate base implementation to allow attaching combo semantics overrides.
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                bool applyComboBreak;
                bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak, out upgradeToPerfect);
                if (!cont) return;
            }

            // Behaviour parity with previous implementation:
            // Previously we forwarded `timeOffset * RELEASE_WINDOW_LENIENCE` to base, which then divided by RELEASE_WINDOW_LENIENCE,
            // resulting in `timeOffset` being used for hit windows.
            double adjustedOffset = timeOffset;

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(adjustedOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(adjustedOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNote : DrawableHoldNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Tail.AllJudged)
            {
                if (Tail.IsHit)
                {
                    bool breakComboFromTailMeh = Tail.Result.Type == HitResult.Meh;

                    ApplyResult(static (r, breakCombo) =>
                    {
                        r.Type = r.Judgement.MaxResult;

                        // In O2Jam hit mode, a Meh on the tail should terminally break combo.
                        // Prevent the parent hold note result from immediately re-increasing combo afterwards.
                        if (breakCombo)
                            r.IsComboHit = false;
                    }, breakComboFromTailMeh);
                }
                else
                    MissForcefully();

                // Make sure that the hold note is fully judged by giving the body a judgement.
                if (!Body.AllJudged)
                    Body.TriggerResult(Tail.IsHit);

                // Important that this is always called when a result is applied.
                Result.ReportHoldState(Time.Current, false);
            }
        }
    }

    public class O2Note : Note
    {
        public O2Note(Note note)
        {
            StartTime = note.StartTime;
            Column = note.Column;
            Samples = note.Samples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }
    }

    public class O2LNHead : HeadNote
    {
    }

    public class O2LNTail : TailNote
    {
        public override double MaximumJudgementOffset => base.MaximumJudgementOffset / RELEASE_WINDOW_LENIENCE;
    }
}
