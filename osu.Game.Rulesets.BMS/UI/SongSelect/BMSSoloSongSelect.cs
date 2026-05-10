// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// BMS-flavoured song select that reuses the entire <see cref="SoloSongSelect"/> stack
    /// (carousel, filter, mod overlay, preview, footer, background) and only overrides
    /// the parts that are BMS-specific:
    /// <list type="bullet">
    /// <item>defaults the active ruleset to BMS so the standard carousel naturally hides non-BMS sets;</item>
    /// <item>on start, reconstructs a <see cref="BMSWorkingBeatmap"/> for the selected external chart and
    /// hands it to <see cref="BmsPlayer"/> via the route configured in <see cref="BMSRulesetConfigManager"/>.</item>
    /// </list>
    /// </summary>
    public partial class BMSSoloSongSelect : SoloSongSelect
    {
        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Default to BMS ruleset so the standard carousel filters non-BMS beatmaps.
            // Users may still switch ruleset from the carousel header if they want to peek at other content.
            Ruleset.Value = new BMSRuleset().RulesetInfo;
        }

        protected override void OnStart()
        {
            // SoloSongSelect's own OnStart pushes a SoloPlayer over the current Beatmap.Value.
            // For BMS beatmaps the Realm-backed working beatmap can't load keysounds / external audio,
            // so we substitute a BMSWorkingBeatmap before the PlayerLoader picks it up.
            //
            // This intentionally does NOT call base.OnStart(): the standard implementation
            // would push a SoloPlayer over the wrong working beatmap.
            try
            {
                if (!tryRebuildBmsWorkingBeatmap(Beatmap.Value, out var bmsWorking))
                {
                    Logger.Log("BMS solo song select: selected beatmap is not a BMS external entry; falling back to default flow.", LoggingTarget.Runtime, LogLevel.Important);
                    base.OnStart();
                    return;
                }

                var route = resolveRoute();

                if (route == BMSGameplayRoute.ManiaCompatibility)
                {
                    Beatmap.Value = new ManiaConvertedWorkingBeatmap(bmsWorking, audioManager);
                    Ruleset.Value = new ManiaRuleset().RulesetInfo;
                }
                else
                {
                    Beatmap.Value = bmsWorking;
                    Ruleset.Value = new BMSNativeRuleset().RulesetInfo;
                }

                this.Push(new PlayerLoader(() => new BmsPlayer()));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BMS solo song select: failed to start gameplay; falling back to base implementation.");
                base.OnStart();
            }
        }

        private BMSGameplayRoute resolveRoute()
        {
            try
            {
                if (rulesetConfigCache.GetConfigFor(new BMSRuleset()) is BMSRulesetConfigManager bmsConfig)
                    return bmsConfig.Get<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute);
            }
            catch (Exception ex)
            {
                Logger.Log($"BMS solo song select: failed to read GameplayRoute config: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return BMSGameplayRoute.ManiaCompatibility;
        }

        private bool tryRebuildBmsWorkingBeatmap(WorkingBeatmap source, out BMSWorkingBeatmap bmsWorking)
        {
            bmsWorking = null!;

            if (!TryResolveExternalChartPath(source, out string chartPath))
                return false;

            BMSChartCache? chartCache = tryLookupChartCache(source.BeatmapInfo.MD5Hash);

            bmsWorking = new BMSWorkingBeatmap(chartPath, audioManager, textures, chartCache);
            return true;
        }

        /// <summary>
        /// Resolve the on-disk chart file path for an external BMS working beatmap.
        /// </summary>
        public static bool TryResolveExternalChartPath(WorkingBeatmap source, out string chartPath)
        {
            var setInfo = source.BeatmapSetInfo;

            IEnumerable<string> setFilenames = setInfo?.Files.Select(f => f.Filename) ?? Enumerable.Empty<string>();

            return TryResolveExternalChartPath(setInfo?.Hash, source.BeatmapInfo.Path, setFilenames, out chartPath);
        }

        /// <summary>
        /// Pure helper for resolving an external BMS chart path from set hash + filename hints.
        /// </summary>
        public static bool TryResolveExternalChartPath(string? setHash, string? beatmapPath, IEnumerable<string> setFilenames, out string chartPath)
        {
            chartPath = string.Empty;

            if (!BMSExternalPath.TryDecode(setHash, out string folderPath))
                return false;

            string? chartFilename = beatmapPath;

            if (string.IsNullOrEmpty(chartFilename))
            {
                chartFilename = setFilenames.FirstOrDefault(name => (name.EndsWith(".bms", StringComparison.OrdinalIgnoreCase)
                                                                     || name.EndsWith(".bme", StringComparison.OrdinalIgnoreCase)
                                                                     || name.EndsWith(".bml", StringComparison.OrdinalIgnoreCase)
                                                                     || name.EndsWith(".pms", StringComparison.OrdinalIgnoreCase)));
            }

            if (string.IsNullOrEmpty(chartFilename))
                return false;

            string candidate = Path.Combine(folderPath, chartFilename);

            if (!File.Exists(candidate))
                return false;

            chartPath = candidate;
            return true;
        }

        private BMSChartCache? tryLookupChartCache(string? md5Hash)
        {
            if (string.IsNullOrEmpty(md5Hash))
                return null;

            try
            {
                string cacheDir = storage.GetFullPath("bms_cache");
                var manager = BMSBeatmapManager.GetShared(cacheDir);
                return manager.GetChartByHash(md5Hash);
            }
            catch (Exception ex)
            {
                Logger.Log($"BMS solo song select: chart cache lookup failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }
    }
}
