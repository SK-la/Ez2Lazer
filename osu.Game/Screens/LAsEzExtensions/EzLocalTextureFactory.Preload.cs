using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.Screens.LAsEzExtensions
{
    public partial class EzLocalTextureFactory
    {
        #region 预加载系统

        private static readonly string[] preload_components =
        {
            "whitenote", "bluenote", "greennote",
            "noteflare", "noteflaregood", "longnoteflare",
        };

        private static readonly object preload_lock = new object();
        private static bool isPreloading;
        private static bool preloadCompleted;

        public async Task PreloadGameTextures()
        {
            if (preloadCompleted || isPreloading) return;

            lock (preload_lock)
            {
                if (preloadCompleted || isPreloading) return;

                isPreloading = true;
            }

            try
            {
                string currentNoteSetName = noteSetName.Value;
                Logger.Log($"[EzLocalTextureFactory] Starting preload for note set: {currentNoteSetName}",
                    LoggingTarget.Runtime, LogLevel.Debug);

                var preloadTasks = new List<Task>();

                foreach (string component in preload_components)
                {
                    preloadTasks.Add(Task.Run(() => preloadComponent(component, currentNoteSetName)));
                }

                // preloadTasks.Add(Task.Run(preloadStageTextures));

                await Task.WhenAll(preloadTasks).ConfigureAwait(false);

                lock (preload_lock)
                {
                    preloadCompleted = true;
                    isPreloading = false;
                }

                Logger.Log($"[EzLocalTextureFactory] Preload completed for {preload_components.Length} components",
                    LoggingTarget.Runtime, LogLevel.Debug);

                Logger.Log($"[EzLocalTextureFactory] Cache stats after preload: {singleTextureCache.Count} single textures, {global_cache.Count} frame sets",
                    LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Preload failed: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);

                lock (preload_lock)
                {
                    isPreloading = false;
                }
            }
        }

        private void preloadComponent(string component, string noteSetName)
        {
            try
            {
                string cacheKey = $"{noteSetName}_{component}";

                if (global_cache.ContainsKey(cacheKey)) return;

                var frames = loadNotesFrames(component, noteSetName);

                if (frames.Count > 0)
                {
                    var newEntry = new CacheEntry(frames, true);
                    global_cache.TryAdd(cacheKey, newEntry);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Failed to preload {component}: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        // private async Task preloadStageTextures()
        // {
        //     try
        //     {
        //         string currentStageName = stageName.Value;
        //         Logger.Log($"[EzLocalTextureFactory] Preloading stage textures for: {currentStageName}",
        //             LoggingTarget.Runtime, LogLevel.Debug);
        //
        //         var stagePaths = new List<string>
        //         {
        //             $"Stage/{currentStageName}/Stage/fivekey/Body",
        //             $"Stage/{currentStageName}/Stage/GrooveLight",
        //             $"Stage/{currentStageName}/Stage/eightkey/keybase/KeyBase",
        //             $"Stage/{currentStageName}/Stage/eightkey/keypress/KeyBase",
        //             $"Stage/{currentStageName}/Stage/eightkey/keypress/KeyPress",
        //         };
        //
        //         foreach (string path in stagePaths)
        //         {
        //             // For stage textures, skip preloading to avoid conflicts with runtime loading
        //             // var texture = largeTextureStore.Get($"{path}.png");
        //             // if (texture != null)
        //             //     loadedCount++;
        //
        //             Logger.Log($"[EzLocalTextureFactory] Skipping preload for stage texture {path}",
        //                 LoggingTarget.Runtime, LogLevel.Debug);
        //
        //             // Simulate loading delay if needed
        //             // await Task.Delay(10).ConfigureAwait(false);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.Log($"[EzLocalTextureFactory] Stage texture preload failed: {ex.Message}",
        //             LoggingTarget.Runtime, LogLevel.Error);
        //     }
        // }

        private void resetPreloadState()
        {
            lock (preload_lock)
            {
                preloadCompleted = false;
                isPreloading = false;
            }
        }

        #endregion
    }
}
