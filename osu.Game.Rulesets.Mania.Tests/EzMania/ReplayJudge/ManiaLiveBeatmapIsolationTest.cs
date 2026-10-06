// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 绑定语义回归：仿真必须判定在 provider 产出的独立副本上，调用方实例（可能是本局 live 实例，
    /// 也可能是被多个 ghost 共享的实例）的 HitWindows / Judgement 不得被改写；
    /// 万一仍有结果越界，Ez 判定必须降级并计数，而不是抛 <see cref="System.InvalidOperationException"/>。
    /// </summary>
    [TestFixture]
    public class ManiaLiveBeatmapIsolationTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown()
        {
            ReplayJudgeTestConfig.ResetGlobalConfig();
            ManiaJudgeHotPathTrace.Clear();
        }

        [Test]
        public void TestServiceSimulationUsesCopyAndLeavesCallerInstanceUntouched()
        {
            var (score, callerBeatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadPerfect();
            score.ScoreInfo.BeatmapInfo = callerBeatmap.BeatmapInfo;

            // 模拟局内：调用方实例按 EZ2AC 绑定（窗口 + Judgement）。
            ManiaBeatmapBinding.BindForLive(callerBeatmap, environment);

            var hold = callerBeatmap.HitObjects.OfType<HoldNote>().Single();
            var headWindows = (ManiaHitWindows)hold.Head.HitWindows!;
            var tick = hold.Ticks![0];

            Assert.That(headWindows.ActiveHitMode, Is.EqualTo(EzEnumHitMode.EZ2AC));
            Assert.That(tick.Judgement, Is.TypeOf<HoldNoteTickJudgement>());

            // autoplay 的 ScoreInfo 尚无嵌入 hitmode → ForStored 回退 Lazer（与崩溃现场一致）。
            var storedEnvironment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);
            Assert.That(storedEnvironment.ManiaHitMode, Is.EqualTo(EzEnumHitMode.Lazer), "前置条件：ForStored 应回退 Lazer");

            var service = new ManiaReplaySessionService();
            service.AttachBeatmaps(createCache(createEz2AcHoldBeatmap));

            var timeline = service.RunTimelineDirectAsync(score, callerBeatmap, ReplayRunPurpose.ForStored).GetAwaiter().GetResult();

            Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0), "副本上的仿真应仍产出判定");

            Assert.That(headWindows.ActiveHitMode, Is.EqualTo(EzEnumHitMode.EZ2AC), "live 判定窗口被仿真改写");
            Assert.That(hold.Head.HitWindows, Is.SameAs(headWindows), "live HitWindows 实例被替换");
            Assert.That(tick.Judgement, Is.TypeOf<HoldNoteTickJudgement>(), "live tick 判定被仿真改写");
            Assert.That(hold.Tail.Judgement, Is.TypeOf<MalodyTailJudgement>(), "live tail 判定被仿真改写");
            Assert.That(ManiaJudgeHotPathTrace.BeatmapRebindConflicts, Is.EqualTo(0), "provider 路径不应产生绑定冲突");
        }

        [Test]
        public void TestServiceWithoutBeatmapSourceKeepsCallerInstance()
        {
            // 无 BeatmapManager 的宿主（测试）没有副本来源，必须沿用调用方实例，而不是静默失败。
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var timeline = new ManiaReplaySessionService()
                           .RunTimelineDirectAsync(score, beatmap, ReplayRunPurpose.ForStored).GetAwaiter().GetResult();

            Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));
        }

        [Test]
        public void TestProviderCachesPerHitModeAndNeverReturnsSourceInstance()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            score.ScoreInfo.BeatmapInfo = beatmap.BeatmapInfo;

            var provider = new ManiaSimulationBeatmapProvider(createCache(createEz2AcHoldBeatmap));

            var lazer = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            var ez2ac = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            var first = provider.TryCreate(score, lazer, CancellationToken.None);
            var sameKey = provider.TryCreate(score, lazer, CancellationToken.None);
            var otherHitMode = provider.TryCreate(score, ez2ac, CancellationToken.None);

            Assert.That(first, Is.Not.Null);
            Assert.That(first, Is.Not.SameAs(beatmap), "副本不得是调用方实例");
            Assert.That(sameKey, Is.SameAs(first), "同键应复用同一副本");
            Assert.That(otherHitMode, Is.Not.SameAs(first), "不同 hitmode 必须拿到不同实例");
            Assert.That(((ManiaHitWindows)otherHitMode!.HitObjects.OfType<HoldNote>().Single().Head.HitWindows!).ActiveHitMode,
                        Is.EqualTo(EzEnumHitMode.EZ2AC));
        }

        [Test]
        public void TestBindForLiveAppliesWindowsAndJudgements()
        {
            // tick 只在全局 EZ2AC 下生成，所以先建谱面再验证绑定把默认的官方 Judgement 换成 EZ2AC 语义。
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac));

            var beatmap = createEz2AcHoldBeatmap();
            var hold = beatmap.HitObjects.OfType<HoldNote>().Single();
            var tick = hold.Ticks![0];

            Assert.That(tick.Judgement, Is.Not.TypeOf<HoldNoteTickJudgement>(), "绑定前应是官方默认 Judgement");

            ManiaBeatmapBinding.BindForLive(beatmap, ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac));

            Assert.That(tick.Judgement, Is.TypeOf<HoldNoteTickJudgement>());
            Assert.That(((ManiaHitWindows)hold.Head.HitWindows!).ActiveHitMode, Is.EqualTo(EzEnumHitMode.EZ2AC));
        }

        [Test]
        public void TestRebindingDifferentHitModeIsReportedAndLastWins()
        {
            // 非 provider 实例（测试注入、调用方自备）沿用既有 last-wins 行为，但必须留下可 grep 的计数。
            var beatmap = createEz2AcHoldBeatmap();
            var headWindows = (ManiaHitWindows)beatmap.HitObjects.OfType<HoldNote>().Single().Head.HitWindows!;

            ManiaBeatmapBinding.BindForSimulation(beatmap, EzEnumHitMode.EZ2AC);
            Assert.That(ManiaJudgeHotPathTrace.BeatmapRebindConflicts, Is.EqualTo(0));

            Assert.DoesNotThrow(() => ManiaBeatmapBinding.BindForSimulation(beatmap, EzEnumHitMode.Lazer));

            Assert.That(headWindows.ActiveHitMode, Is.EqualTo(EzEnumHitMode.Lazer));
            Assert.That(ManiaJudgeHotPathTrace.BeatmapRebindConflicts, Is.EqualTo(1));
        }

        [Test]
        public void TestSameHitModeRebindingIsIdempotentAndSilent()
        {
            var beatmap = createEz2AcHoldBeatmap();

            ManiaBeatmapBinding.BindForSimulation(beatmap, EzEnumHitMode.EZ2AC);
            ManiaBeatmapBinding.BindForSimulation(beatmap, EzEnumHitMode.EZ2AC);

            Assert.That(ManiaJudgeHotPathTrace.BeatmapRebindConflicts, Is.EqualTo(0));
        }

        [Test]
        public void TestDrawableTickDowngradeStaysInsideApplyResultRange()
        {
            // 裸 drawable 未挂树，Time/Clock 不可用，无法直接跑 ApplyResult；这里验证它读取的绑定与降级结果。
            var tick = new DrawableHoldNoteTick(new HoldNoteTick { StartTime = 1000, Column = 0 });

            var judgement = tick.Result.Judgement;
            Assert.That(judgement, Is.TypeOf<IgnoreJudgement>(), "tick 默认绑定官方 IgnoreJudgement");

            // 同 hitmode 下 EZ2AC tick 会给 SliderTailHit —— 正是崩溃现场。
            var sanitized = ManiaEzDrawableJudgement.SanitizeResult(tick, HitResult.SliderTailHit);

            Assert.That(sanitized, Is.Not.EqualTo(HitResult.SliderTailHit));
            Assert.That(sanitized.IsValidHitResult(judgement.MinResult, judgement.MaxResult), Is.True,
                "降级结果必须落在 ApplyResult 的区间校验内，否则仍会抛 InvalidOperationException");
        }

        [Test]
        public void TestSanitizeResultKeepsLegalResults()
        {
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new HoldNoteTickJudgement(), HitResult.SliderTailHit, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.SliderTailHit));
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new HoldNoteTickJudgement(), HitResult.IgnoreMiss, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.IgnoreMiss));
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new MalodyTailJudgement(), HitResult.IgnoreHit, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.IgnoreHit));
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(null, HitResult.SliderTailHit, null),
                        Is.EqualTo(HitResult.SliderTailHit), "未绑定 Judgement 时不干预");

            Assert.That(ManiaJudgeHotPathTrace.JudgementResultDowngrades, Is.EqualTo(0), "合法结果不得计数");
        }

        [Test]
        public void TestSanitizeResultDowngradesOutOfRangeResults()
        {
            // 崩溃现场 1：tick 被污染成官方 IgnoreJudgement，EZ2AC 内核给出 SliderTailHit。
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new IgnoreJudgement(), HitResult.SliderTailHit, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.IgnoreHit));

            // 崩溃现场 2：tail 被污染成官方 ManiaJudgement，EZ2AC 内核给出 IgnoreHit；IgnoreHit 不在区间内，退回 MinResult。
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new ManiaJudgement(), HitResult.IgnoreHit, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.Miss));

            // IgnoreJudgement 下 EZ2AC 的 IgnoreMiss 仍合法。
            Assert.That(ManiaEzDrawableJudgement.SanitizeResult(new IgnoreJudgement(), HitResult.IgnoreMiss, EzEnumHitMode.EZ2AC),
                        Is.EqualTo(HitResult.IgnoreMiss));

            Assert.That(ManiaJudgeHotPathTrace.JudgementResultDowngrades, Is.EqualTo(2), "降级兜底必须可计数");
        }

        private static TestBeatmap createEz2AcHoldBeatmap()
        {
            var ruleset = new ManiaRuleset();
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { new HoldNote { StartTime = 1000, Duration = 1000, Column = 0 } },
                ControlPointInfo = controlPoints,
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }

        private static IWorkingBeatmapCache createCache(Func<IBeatmap> factory)
            => new FixedBeatmapCache(new FactoryWorkingBeatmap(factory(), factory));

        private sealed class FixedBeatmapCache : IWorkingBeatmapCache
        {
            private readonly WorkingBeatmap working;

            public FixedBeatmapCache(WorkingBeatmap working)
            {
                this.working = working;
            }

            public WorkingBeatmap GetWorkingBeatmap(BeatmapInfo beatmapInfo) => working;

            public void Invalidate(BeatmapSetInfo beatmapSetInfo)
            {
            }

            public void Invalidate(BeatmapInfo beatmapInfo)
            {
            }
        }

        /// <summary>每次转换都产出一个新实例，模拟 <c>BeatmapManager</c> 的真实行为（真库永远是重新解码）。</summary>
        private sealed class FactoryWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly Func<IBeatmap> factory;

            public FactoryWorkingBeatmap(IBeatmap source, Func<IBeatmap> factory)
                : base(source)
            {
                this.factory = factory;
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token) => factory();
        }
    }
}
