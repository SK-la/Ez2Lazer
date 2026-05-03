// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania成绩的<see cref="HitEvent"/>生成器，通过将成绩的回放输入重新评估与提供的可玩谱面进行比较来生成<see cref="HitEvent"/>。
    /// <para>这个生成器主要用于结果/统计用途，其中<see cref="ScoreInfo.HitEvents"/>没有被持久化。</para>
    /// <para>上游通过反射注册到<see cref="EzScoreReloadBridge"/>，因此无法直接查看调用源。</para>
    /// </summary>
    public sealed class ManiaScoreHitEventGenerator : IScoreHitEventGenerator
    {
        public static ManiaScoreHitEventGenerator Instance { get; } = new ManiaScoreHitEventGenerator();

        static ManiaScoreHitEventGenerator()
        {
            EzScoreReloadBridge.RegisterImplementation("mania", Instance);
            EzScoreReloadBridge.RegisterImplementation("3", Instance);
        }

        public bool Validate(Score score)
        {
            if (score.ScoreInfo.Ruleset.OnlineID != 3)
                return false;

            var replay = score.Replay;

            if (replay == null || replay.Frames.Count == 0)
                return false;

            if (replay.Frames.Any(f => f is not ManiaReplayFrame))
                return false;

            return true;
        }

        /// <summary>
        /// Instance implementation of generator.
        /// </summary>
        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Replay replay = score.Replay;
            EzEnumHitMode hitMode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            bool poorEnabled = GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.BmsPoorHitResultEnable);
            bool pillModeEnabled = score.ScoreInfo.Mods.OfType<ManiaModO2Judgement>().Any(mod => mod.PillMode.Value);

            var frames = replay.Frames.Cast<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();
            var hitWindowHelper = new HitModeHelper(hitMode)
            {
                OverallDifficulty = playableBeatmap.Difficulty.OverallDifficulty,
                BPM = getBpmAtTime(playableBeatmap, 0, hitMode),
            };

            // Build per-column input transitions.
            var pressTimesByColumn = new List<double>[32];
            var releaseTimesByColumn = new List<double>[32];

            for (int i = 0; i < pressTimesByColumn.Length; i++)
            {
                pressTimesByColumn[i] = new List<double>();
                releaseTimesByColumn[i] = new List<double>();
            }

            HashSet<ManiaAction> last = new HashSet<ManiaAction>();

            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = new HashSet<ManiaAction>(frame.Actions);

                foreach (var action in current)
                {
                    if (last.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0 && column < pressTimesByColumn.Length)
                        pressTimesByColumn[column].Add(frame.Time);
                }

                foreach (var action in last)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0 && column < releaseTimesByColumn.Length)
                        releaseTimesByColumn[column].Add(frame.Time);
                }

                last = current;
            }

            // If keys are still held at the end of replay, treat them as released at the last frame time.
            if (last.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in last)
                {
                    int column = (int)action;
                    if (column >= 0 && column < releaseTimesByColumn.Length)
                        releaseTimesByColumn[column].Add(endTime);
                }
            }

            // Map tail -> head to support capping (combo-break conditions).
            var headByTail = new Dictionary<TailNote, HeadNote>();

            foreach (var hitObject in playableBeatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is HoldNote hold)
                    headByTail[hold.Tail] = hold.Head;
            }

            var targets = new List<HitObject>();

            foreach (var hitObject in playableBeatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                collectJudgementTargets(hitObject, targets, cancellationToken);
            }

            // Ensure deterministic ordering.
            targets.Sort((a, b) =>
            {
                int timeComparison = a.StartTime.CompareTo(b.StartTime);
                if (timeComparison != 0)
                    return timeComparison;

                int colA = (a as IHasColumn)?.Column ?? 0;
                int colB = (b as IHasColumn)?.Column ?? 0;
                return colA.CompareTo(colB);
            });

            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            var hitEvents = new List<HitEvent>(targets.Count);
            HitObject? lastHitObject = null;

            // Track head hit results for later tail capping.
            var headWasHit = new Dictionary<HeadNote, bool>();
            int o2PillCount = 0;
            int o2CoolCombo = 0;

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                    continue;

                if (target is not IHasColumn hasColumn)
                    continue;

                int column = hasColumn.Column;
                if (column < 0 || column >= pressTimesByColumn.Length)
                    continue;

                bool isTail = target is TailNote;
                bool useTailReleaseLenience = isTail && usesTailReleaseLenience(hitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, target.StartTime, hitMode);

                double missWindow = hitWindowHelper.WindowFor(HitResult.Miss) * lenienceFactor;

                List<double> times = isTail ? releaseTimesByColumn[column] : pressTimesByColumn[column];

                int idx = times.FindIndex(t => t >= target.StartTime - missWindow && t <= target.StartTime + missWindow);

                double timeOffsetForJudgement = 0;
                HitResult result;

                bool holdBreak = false;

                if (idx >= 0)
                {
                    double eventTime = times[idx];
                    times.RemoveAt(idx);

                    double rawOffset = eventTime - target.StartTime;
                    if (isTail && rawOffset < 0)
                        holdBreak = true;

                    hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, eventTime, hitMode);

                    timeOffsetForJudgement = useTailReleaseLenience ? rawOffset / TailNote.RELEASE_WINDOW_LENIENCE : rawOffset;

                    bool headHit = false;

                    if (target is TailNote tail && headByTail.TryGetValue(tail, out var headNote))
                        headHit = headWasHit.TryGetValue(headNote, out bool wasHit) && wasHit;

                    result = evaluateResult(target, hitWindowHelper, hitMode, poorEnabled, timeOffsetForJudgement, rawOffset, holdBreak, headHit,
                        playableBeatmap, eventTime, pillModeEnabled, ref o2PillCount, ref o2CoolCombo);

                    if (target is HeadNote head)
                        headWasHit[head] = result.IsHit();
                }
                else
                {
                    // No matching input event. Treat as a miss.
                    result = HitResult.Miss;

                    if (target is HeadNote head)
                        headWasHit[head] = false;
                }

                hitEvents.Add(new HitEvent(timeOffsetForJudgement, gameplayRate, result, target, lastHitObject, null));
                lastHitObject = target;
            }

            return hitEvents;
        }

        // TODO: Ez2Ac没有实现
        private static HitResult evaluateResult(HitObject target, HitModeHelper hitModeHelper, EzEnumHitMode hitMode, bool poorEnabled,
                                                double timeOffsetForJudgement, double rawOffset, bool holdBreak, bool headHit,
                                                IBeatmap playableBeatmap, double eventTime, bool pillModeEnabled, ref int o2PillCount, ref int o2CoolCombo)
        {
            double absOffset = Math.Abs(timeOffsetForJudgement);
            bool isTail = target is TailNote;

            if (hitMode == EzEnumHitMode.Lazer)
            {
                HitResult result = target.HitWindows?.ResultFor(timeOffsetForJudgement) ?? HitResult.None;

                if (result == HitResult.None)
                    result = HitResult.Miss;

                if (isTail && result > HitResult.Meh && (!headHit || holdBreak))
                    result = HitResult.Meh;

                return result;
            }

            if (isBmsLike(hitMode))
            {
                double badLate = hitModeHelper.WindowFor(BMSJudgeMapping.Bad, false);

                HitResult result = hitModeHelper.ResultFor(timeOffsetForJudgement);

                if (poorEnabled && result == HitResult.None)
                    result = BMSJudgeMapping.KPoor;

                if (result == HitResult.None && (absOffset > badLate || (isTail && holdBreak)))
                    return BMSJudgeMapping.Poor;

                return result;
            }

            if (hitMode == EzEnumHitMode.O2Jam)
            {
                HitResult result = hitModeHelper.ResultFor(timeOffsetForJudgement);

                if (result == HitResult.None)
                    result = HitResult.Miss;

                if (pillModeEnabled)
                    applyO2PillLogic(Math.Abs(rawOffset), getBpmAtTime(playableBeatmap, eventTime, hitMode), ref o2PillCount, ref o2CoolCombo, ref result);

                if (isTail && (holdBreak || !headHit))
                    result = HitResult.Miss;

                return result;
            }

            if ((hitMode == EzEnumHitMode.Malody_E || hitMode == EzEnumHitMode.Malody_B) && isTail)
            {
                if (!headHit)
                    return HitResult.Miss;

                return rawOffset > 0 || Math.Abs(rawOffset) <= hitModeHelper.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE
                    ? HitResult.Perfect
                    : HitResult.Miss;
            }

            HitResult defaultResult = hitModeHelper.ResultFor(timeOffsetForJudgement);

            if (defaultResult == HitResult.None)
                defaultResult = HitResult.Miss;

            if (isTail && defaultResult > HitResult.Meh && (!headHit || holdBreak))
                defaultResult = HitResult.Meh;

            return defaultResult;
        }

        private static void applyO2PillLogic(double absOffset, double bpm, ref int pillCount, ref int coolCombo, ref HitResult result)
        {
            double coolRange = O2HitModeExtension.BASE_COOL / bpm;
            double goodRange = O2HitModeExtension.BASE_GOOD / bpm;
            double badRange = O2HitModeExtension.BASE_BAD / bpm;

            if (absOffset <= coolRange)
            {
                coolCombo++;

                if (coolCombo >= 15)
                {
                    coolCombo = 0;
                    pillCount = Math.Clamp(pillCount + 1, 0, 5);
                }

                return;
            }

            if (absOffset <= goodRange)
            {
                coolCombo = 0;
                return;
            }

            if (absOffset > badRange)
                return;

            coolCombo = 0;

            if (pillCount <= 0)
                return;

            pillCount = Math.Clamp(pillCount - 1, 0, 5);
            result = HitResult.Perfect;
        }

        private static bool isBmsLike(EzEnumHitMode hitMode)
            => hitMode == EzEnumHitMode.IIDX_HD || hitMode == EzEnumHitMode.LR2_HD || hitMode == EzEnumHitMode.Raja_NM;

        private static bool usesTailReleaseLenience(EzEnumHitMode hitMode)
            => hitMode == EzEnumHitMode.Lazer || hitMode == EzEnumHitMode.Classic;

        private static double getBpmAtTime(IBeatmap beatmap, double time, EzEnumHitMode hitMode)
        {
            double bpm = beatmap.ControlPointInfo.TimingPointAt(time).BPM;

            if (bpm <= 0)
                bpm = beatmap.BeatmapInfo.BPM;

            if (bpm <= 0)
                bpm = 120;

            if (hitMode == EzEnumHitMode.O2Jam)
                return Math.Max(bpm, 75.0);

            return bpm;
        }

        private static void collectJudgementTargets(HitObject hitObject, List<HitObject> targets, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty) && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
                targets.Add(hitObject);

            foreach (var nested in hitObject.NestedHitObjects)
                collectJudgementTargets(nested, targets, cancellationToken);
        }
    }
}
