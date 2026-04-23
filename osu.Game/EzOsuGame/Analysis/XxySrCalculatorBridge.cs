// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Analysis
{
    public interface IXxySrCalculator
    {
        bool TryCalculateXxySr(IBeatmap beatmap, double clockRate, out double sr);
    }

    internal static class XxySrCalculatorBridge
    {
        private static int invokeFailCount;

        public static bool TryCalculate(IRulesetInfo rulesetInfo, IBeatmap beatmap, double clockRate, out double sr)
        {
            sr = 0;

            if (rulesetInfo.CreateInstance() is not IXxySrCalculator calculator)
                return false;

            try
            {
                return calculator.TryCalculateXxySr(beatmap, clockRate, out sr);
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref invokeFailCount) <= 10)
                    Logger.Error(ex, $"xxy_SR bridge invoke exception. ruleset={rulesetInfo.ShortName}, beatmapType={beatmap.GetType().FullName}, clockRate={clockRate}", Ez2ConfigManager.LOGGER_NAME);

                return false;
            }
        }
    }
}
