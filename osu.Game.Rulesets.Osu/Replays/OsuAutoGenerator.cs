// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays
{
    public class OsuAutoGenerator : OsuAutoplayCore
    {
        public new const double MIN_FRAME_SEPARATION_FOR_ALTERNATING = OsuAutoplayCore.MIN_FRAME_SEPARATION_FOR_ALTERNATING;

        /// <summary>
        /// 本生成器在拒绝继续分配之前最多产出的帧数。
        /// </summary>
        /// <remarks>
        /// 一小时的密集谱面大约只要二十多万帧，因此留出很大余量，同时拦住每个要数百万帧的滑条。
        /// </remarks>
        public const int MAX_GENERATED_FRAME_COUNT = 1_000_000;

        public OsuAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(beatmap, mods)
        {
        }

        public override Replay Generate()
        {
            if (Beatmap.HitObjects.Count == 0)
                return Replay;

            ensureFrameBudgetIsReasonable();
            return base.Generate();
        }

        /// <summary>
        /// 拒绝时长会导致无法分配帧数的谱面。
        /// </summary>
        /// <remarks>
        /// 在创建任何帧之前运行。分配到那种规模会在运行时内耗尽内存并以 execution-engine 失败结束进程，
        /// 因此必须事先拒绝，而不能事后才发现。
        /// </remarks>
        private void ensureFrameBudgetIsReasonable()
        {
            double estimated = 0;

            foreach (OsuHitObject h in Beatmap.HitObjects)
            {
                double duration = h.GetEndTime() - h.StartTime;

                if (duration > 0)
                    estimated += duration / GetFrameDelay(h.StartTime);

                if (estimated > MAX_GENERATED_FRAME_COUNT)
                    throw new AutoplayFrameLimitExceededException(estimated, MAX_GENERATED_FRAME_COUNT);
            }
        }

        protected override void AddSliderBodyFrames(Slider slider, OsuAction action)
        {
            if (slider.Duration <= 0)
                return;

            for (double j = GetFrameDelay(slider.StartTime); j < slider.Duration; j += GetFrameDelay(slider.StartTime + j))
            {
                Vector2 pos = slider.StackedPositionAt(j / slider.Duration);

                if (!AcceptCursorPosition(pos))
                    continue;

                AddFrameToReplay(new OsuReplayFrame(slider.StartTime + j, pos, action));
            }
        }
    }
}
