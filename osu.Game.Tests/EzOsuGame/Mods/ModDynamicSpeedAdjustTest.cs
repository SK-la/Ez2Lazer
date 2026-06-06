// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Mods.CommunityMod;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.EzOsuGame.Mods
{
    [TestFixture]
    public class ModDynamicSpeedAdjustTest
    {
        [Test]
        public void TestShowSpeedLineDefaultsTrueOnDynamicSpeedMods()
        {
            Assert.That(new ModNiceBPM().ShowSpeedLine.Value, Is.True);
            Assert.That(new ModAccuracyAdaptive().ShowSpeedLine.Value, Is.True);
            Assert.That(new ModHealthAdaptive().ShowSpeedLine.Value, Is.True);
        }

        [Test]
        public void TestLinkSpeedHudDefaultsTrueOnDynamicSpeedMods()
        {
            Assert.That(new ModNiceBPM().LinkSpeedHUD.Value, Is.True);
            Assert.That(new ModAccuracyAdaptive().LinkSpeedHUD.Value, Is.True);
            Assert.That(new ModHealthAdaptive().LinkSpeedHUD.Value, Is.True);
        }

        [TestCase(typeof(ModNiceBPM))]
        [TestCase(typeof(ModAccuracyAdaptive))]
        [TestCase(typeof(ModHealthAdaptive))]
        public void TestGameplaySpeedDampPropagatesForDynamicMods(System.Type modType)
        {
            var mod = (ModDynamicSpeedAdjust)System.Activator.CreateInstance(modType)!;
            mod.GameplaySpeed.Value = 1.0;

            for (int i = 0; i < 120; i++)
                mod.GameplaySpeed.Value = Interpolation.DampContinuously(mod.GameplaySpeed.Value, 1.12, 50, 16);

            Assert.That(mod.GameplaySpeed.Value, Is.GreaterThan(1.05).Within(0.001));
            Assert.That(mod.SpeedChange.Value, Is.EqualTo(mod.GameplaySpeed.Value).Within(0.011));
        }

        [Test]
        public void TestNiceBpmStableRateFactorsUpdateTargetRate()
        {
            var mod = new ModNiceBPM();
            mod.EnableDynamicBPM.Value = true;

            var recentRatesField = typeof(ModNiceBPM).GetField("recentRates", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var targetRateField = typeof(ModNiceBPM).GetField("targetRate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var updateTargetRate = typeof(ModNiceBPM).GetMethod("updateTargetRate", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var recentRates = (List<double>)recentRatesField.GetValue(mod)!;
            recentRates.Clear();
            recentRates.AddRange(Enumerable.Repeat(1.11d, 8));

            targetRateField.SetValue(mod, 1.0d);
            updateTargetRate.Invoke(mod, null);

            double targetRate = (double)targetRateField.GetValue(mod)!;
            Assert.That(targetRate, Is.GreaterThan(1.0).Within(0.001));
        }

        [Test]
        public void TestRateChangeStepDefaultAndBounds()
        {
            var mod = new ModNiceBPM();

            Assert.That(mod.RateChangeStep.Value, Is.EqualTo(0.002).Within(0.0001));
            Assert.That(mod.RateChangeStep.MinValue, Is.EqualTo(0.001).Within(0.0001));
            Assert.That(mod.RateChangeStep.MaxValue, Is.EqualTo(0.1).Within(0.0001));
        }

        [Test]
        public void TestNiceBpmRateChangeClampedToStep()
        {
            var mod = new ModNiceBPM();
            mod.RateChangeStep.Value = 0.002;

            var getRelativeRateChange = typeof(ModNiceBPM).GetMethod("getRelativeRateChange", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var precedingEndTimesField = typeof(ModNiceBPM).GetField("precedingEndTimes", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var hitObject = new HitObject { StartTime = 1000 };
            ((Dictionary<HitObject, double>)precedingEndTimesField.GetValue(mod)!).Add(hitObject, 900);

            var earlyHit = new JudgementResult(hitObject, new Judgement()) { Type = HitResult.Good };
            earlyHit.TimeOffset = -50;

            double hitFactor = (double)getRelativeRateChange.Invoke(mod, new object[] { earlyHit })!;
            Assert.That(hitFactor, Is.EqualTo(1.002).Within(0.0001));
        }

        [Test]
        public void TestNiceBpmMissSlowdownEveryThreeMisses()
        {
            var mod = new ModNiceBPM();
            mod.MissThreshold.Value = 3;
            mod.RateChangeStep.Value = 0.002;

            var getRelativeRateChange = typeof(ModNiceBPM).GetMethod("getRelativeRateChange", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var missCountField = typeof(ModNiceBPM).GetField("currentMissCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var miss = new JudgementResult(new HitObject(), new Judgement()) { Type = HitResult.Miss };

            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(1.0));
            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(1.0));
            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(0.998).Within(0.0001));
            Assert.That(missCountField.GetValue(mod), Is.EqualTo(0));

            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(1.0));
            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(1.0));
            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(0.998).Within(0.0001));
            Assert.That(missCountField.GetValue(mod), Is.EqualTo(0));
        }

        [Test]
        public void TestNiceBpmHitResetsMissAccumulator()
        {
            var mod = new ModNiceBPM();
            mod.MissThreshold.Value = 3;

            var getRelativeRateChange = typeof(ModNiceBPM).GetMethod("getRelativeRateChange", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var missCountField = typeof(ModNiceBPM).GetField("currentMissCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var precedingEndTimesField = typeof(ModNiceBPM).GetField("precedingEndTimes", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var hitObject = new HitObject { StartTime = 1000 };
            ((Dictionary<HitObject, double>)precedingEndTimesField.GetValue(mod)!).Add(hitObject, 900);

            var miss = new JudgementResult(hitObject, new Judgement()) { Type = HitResult.Miss };
            var good = new JudgementResult(hitObject, new Judgement()) { Type = HitResult.Good };
            good.TimeOffset = 0;

            getRelativeRateChange.Invoke(mod, new object[] { miss });
            getRelativeRateChange.Invoke(mod, new object[] { miss });
            Assert.That(missCountField.GetValue(mod), Is.EqualTo(2));

            getRelativeRateChange.Invoke(mod, new object[] { good });
            Assert.That(missCountField.GetValue(mod), Is.EqualTo(0));

            getRelativeRateChange.Invoke(mod, new object[] { miss });
            Assert.That(getRelativeRateChange.Invoke(mod, new object[] { miss }), Is.EqualTo(1.0));
        }

        [Test]
        public void TestNiceBpmJudgementFilter()
        {
            var mod = new ModNiceBPM();
            mod.EnableDynamicBPM.Value = true;

            var shouldProcessResult = typeof(ModNiceBPM).GetMethod("shouldProcessResult", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var precedingEndTimesField = typeof(ModNiceBPM).GetField("precedingEndTimes", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var hitObject = new HitObject { StartTime = 1000 };
            ((Dictionary<HitObject, double>)precedingEndTimesField.GetValue(mod)!).Add(hitObject, 0);

            Assert.That(shouldProcessResult.Invoke(mod, new object[] { createResult(hitObject, HitResult.Good) }), Is.True);
            Assert.That(shouldProcessResult.Invoke(mod, new object[] { createResult(hitObject, HitResult.Great) }), Is.False);
            Assert.That(shouldProcessResult.Invoke(mod, new object[] { createResult(hitObject, HitResult.Perfect) }), Is.False);
        }

        private static JudgementResult createResult(HitObject hitObject, HitResult type)
        {
            return new JudgementResult(hitObject, new Judgement())
            {
                Type = type,
            };
        }
    }
}
