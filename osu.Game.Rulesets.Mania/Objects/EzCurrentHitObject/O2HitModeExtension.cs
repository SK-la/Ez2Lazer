// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    // 代码改编自YuLiangSSS提供的ManiaModO2Judgement
    public static class O2HitModeExtension
    {
        public const double COOL = 7500.0;
        public const double GOOD = 22500.0;
        public const double BAD = 31250.0;
        public const double DEFAULT_BPM = 200;

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
        public static double NowBeatmapBPM = 200;

        /// <summary>
        /// 统一的 Pill 判定逻辑：将原本分散在各 Drawable 的重复实现合并到这里。
        /// 返回值：true 表示继续执行后续判定逻辑；false 表示应中断后续判定（保留以便未来扩展）。
        /// out 参数 `applyComboBreak`：当命中落入 Bad 范围时为 true，调用者应先应用一个 <see cref="HitResult.ComboBreak"/>（不影响后续基础判定）。
        /// </summary>
        public static bool PillCheck(double timeOffset, out bool applyComboBreak)
        {
            applyComboBreak = false;

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
                // 在 Bad 范围时应先应用一次 ComboBreak（由调用者负责实际应用），然后继续基础判定流程。
                applyComboBreak = true;

                if (PillCount.Value > 0)
                {
                    PillCount.Value--;
                }
            }

            return true;
        }
    }

    public partial class O2DrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }

        public bool PillCheck(double timeOffset)
        {
            bool applyComboBreak;
            bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak);

            if (applyComboBreak)
                ApplyResult(GetCappedResult(HitResult.ComboBreak));

            return cont;
        }
    }

    public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }

        public bool PillCheck(double timeOffset)
        {
            bool applyComboBreak;
            bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak);

            if (applyComboBreak)
                ApplyResult(GetCappedResult(HitResult.ComboBreak));

            return cont;
        }
    }

    public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset * TailNote.RELEASE_WINDOW_LENIENCE);
        }

        public bool PillCheck(double timeOffset)
        {
            bool applyComboBreak;
            bool cont = O2HitModeExtension.PillCheck(timeOffset, out applyComboBreak);

            if (applyComboBreak)
                ApplyResult(GetCappedResult(HitResult.ComboBreak));

            return cont;
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
