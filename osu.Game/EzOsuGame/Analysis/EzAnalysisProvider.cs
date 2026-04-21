// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Analysis
{
    [Flags]
    public enum EzAnalysisScope
    {
        None = 0,
        XxySr = 1 << 0,
        RulesetSpecificRadarData = 1 << 1,
    }

    public readonly record struct EzAnalysisRequest(IBeatmap Beatmap, double ClockRate, EzAnalysisScope RequestedScopes = EzAnalysisScope.None);

    public readonly record struct EzAnalysisField<TValue>(string Key, EzAnalysisScope Scope)
    {
        public Type ValueType => typeof(TValue);
    }

    public interface IEzAnalysis
    {
        bool TryGetValue<TValue>(EzAnalysisField<TValue> field, out TValue value);
    }

    public interface IEzAnalysisProvider
    {
        bool TryCompute(in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis);
    }

    public static class EzAnalysisFields
    {
        public static readonly EzAnalysisField<double> XXY_SR = new EzAnalysisField<double>("xxy_sr", EzAnalysisScope.XxySr);

        public static readonly EzAnalysisField<EzRulesetSpecificRadarResult> RULESET_SPECIFIC_RADAR_RESULT
            = new EzAnalysisField<EzRulesetSpecificRadarResult>("ruleset_specific_radar_result", EzAnalysisScope.RulesetSpecificRadarData);
    }

    public sealed class EzAnalysisBag : IEzAnalysis
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public EzAnalysisBag Set<TValue>(EzAnalysisField<TValue> field, TValue value)
        {
            values[field.Key] = value!;
            return this;
        }

        public bool TryGetValue<TValue>(EzAnalysisField<TValue> field, out TValue value)
        {
            if (values.TryGetValue(field.Key, out object? rawValue) && rawValue is TValue typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }
    }

    internal static class EzAnalysisProviderBridge
    {
        private static int createFailCount;
        private static int invokeFailCount;

        public static bool HasAnalysisProvider(IRulesetInfo rulesetInfo) => TryCreateProvider(rulesetInfo, out _);

        public static bool TryCreateProvider(IRulesetInfo rulesetInfo, out IEzAnalysisProvider provider)
        {
            provider = null!;

            try
            {
                if (rulesetInfo.CreateInstance().CreateEzAnalysisProvider() is not IEzAnalysisProvider createdProvider)
                    return false;

                provider = createdProvider;
                return true;
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref createFailCount) <= 10)
                    Logger.Error(ex, $"Ez analysis provider create exception. ruleset={rulesetInfo.ShortName}", Ez2ConfigManager.LOGGER_NAME);

                return false;
            }
        }

        public static bool TryCompute(IRulesetInfo rulesetInfo, in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis)
        {
            analysis = null!;

            if (!TryCreateProvider(rulesetInfo, out IEzAnalysisProvider provider))
                return false;

            try
            {
                return provider.TryCompute(request, cancellationToken, out analysis);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref invokeFailCount) <= 10)
                {
                    Logger.Error(ex,
                        $"Ez analysis provider invoke exception. ruleset={rulesetInfo.ShortName}, beatmapType={request.Beatmap.GetType().FullName}, clockRate={request.ClockRate}, scopes={request.RequestedScopes}",
                        Ez2ConfigManager.LOGGER_NAME);
                }

                return false;
            }
        }

        public static bool TryGetValue<TValue>(IRulesetInfo rulesetInfo, in EzAnalysisRequest request, EzAnalysisField<TValue> field, CancellationToken cancellationToken,
                                               out TValue value)
        {
            EzAnalysisRequest scopedRequest = request with { RequestedScopes = request.RequestedScopes | field.Scope };

            if (TryCompute(rulesetInfo, scopedRequest, cancellationToken, out IEzAnalysis analysis) && analysis.TryGetValue(field, out value))
                return true;

            value = default!;
            return false;
        }
    }
}
