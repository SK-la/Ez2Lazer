// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame
{
    public partial class EzLocalTextureFactory
    {
        #region 预加载系统

        private static readonly string[] note_color_prefixes = { "white", "blue", "green" };

        private static readonly string[] note_suffix_components =
        {
            "note",
            "longnote/head",
            "longnote/tail",
            "longnote/middle",
            "longnote/body",
        };

        private static readonly string[] shared_note_components =
        {
            "noteflare",
            "noteflaregood",
            "longnoteflare",
            "longnote/body",
            "longnote/head",
            "longnote/tail",
            "whitenote",
            "bluenote",
            "greennote",
        };

        private static readonly string[] key_components = { "KeyBase", "KeyPress" };
        private static readonly string[] key_suffixes = { "0", "1", "2" };

        private volatile bool isPreloading;
        private volatile bool preloadCompleted;
        private string? completedPreloadKey;
        private Task? preloadTask;

        /// <summary>
        /// 当前 NoteSet/Stage 是否已完成解码缓存预热。
        /// </summary>
        public bool IsPreloadReadyForCurrentSettings
        {
            get
            {
                string key = buildPreloadKey();
                return preloadCompleted && completedPreloadKey == key && !isPreloading;
            }
        }

        /// <summary>
        /// 预热当前 note set / stage 的常用帧到 <see cref="EzTextureUsage.AnimationSafe"/> / Large 缓存。
        /// 仅做 Get 解码入缓存，不强制数百次 GPU 上传（避免堵死 PlayerLoader）。
        /// </summary>
        public Task PreloadGameTextures()
        {
            string key = buildPreloadKey();

            if (preloadCompleted && completedPreloadKey == key)
                return Task.CompletedTask;

            if (preloadTask != null && !preloadTask.IsCompleted)
                return preloadTask;

            preloadTask = runPreloadAsync(key);
            return preloadTask;
        }

        private string buildPreloadKey()
            => $"{noteSetName.Value}|{stageName.Value}";

        private async Task runPreloadAsync(string key)
        {
            isPreloading = true;
            preloadCompleted = false;

            try
            {
                Logger.Log($"[EzLocalTextureFactory] Starting preload for key: {key}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                // 解码/入 TextureStore 在线程池做，避免卡更新线程；不批量 LoadComponentAsync 塞满上传队列。
                int count = await Task.Run(collectAndWarmFrames).ConfigureAwait(false);

                completedPreloadKey = key;
                preloadCompleted = true;
                Logger.Log($"[EzLocalTextureFactory] Preload completed for {count} frames (key={key})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Preload failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                completedPreloadKey = key;
                preloadCompleted = true;
            }
            finally
            {
                isPreloading = false;
            }
        }

        private int collectAndWarmFrames()
        {
            var frames = collectPreloadFrames();

            foreach (var (path, usage) in frames)
                resource.Get(path, usage);

            return frames.Count;
        }

        private List<(string Path, EzTextureUsage Usage)> collectPreloadFrames()
        {
            var frames = new List<(string, EzTextureUsage)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string noteSet = noteSetName.Value ?? "default";
            string stage = stageName.Value ?? "default";

            void add(string path, EzTextureUsage usage)
            {
                if (seen.Add($"{(int)usage}:{path}"))
                    frames.Add((path, usage));
            }

            foreach (string color in note_color_prefixes)
            {
                foreach (string suffix in note_suffix_components)
                    collectIndexedFrames(add, $"note/{noteSet}/{color}{suffix}", EzTextureUsage.AnimationSafe);
            }

            foreach (string component in shared_note_components)
                collectIndexedFrames(add, $"note/{noteSet}/{component}", EzTextureUsage.AnimationSafe);

            collectSingle(add, $"note/{noteSet}/JudgementLine", EzTextureUsage.AnimationSafe);

            string stageRoot = $"Stage/{stage}/Stage";
            collectStageSequence(add, $"{stageRoot}/eightkey/Body");
            collectStageSequence(add, $"{stageRoot}/GrooveLight");
            collectStageSequence(add, $"{stageRoot}/{stage}_OverObject/{stage}_OverObject");

            foreach (string keyComponent in key_components)
            {
                foreach (string suffix in key_suffixes)
                {
                    string[] bases =
                    {
                        $"{stageRoot}/eightkey/keybase/{keyComponent}",
                        $"{stageRoot}/eightkey/keypress/{keyComponent}",
                        $"{stageRoot}/eightkey/keybase/{keyComponent}_{suffix}",
                        $"{stageRoot}/eightkey/keypress/{keyComponent}_{suffix}",
                    };

                    foreach (string basePath in bases)
                    {
                        collectKeyedFrames(add, basePath);
                        collectSingle(add, basePath, EzTextureUsage.AnimationSafe);
                    }
                }
            }

            return frames;
        }

        private void collectIndexedFrames(Action<string, EzTextureUsage> add, string path, EzTextureUsage usage)
        {
            for (int i = 0; i < max_frames_to_load; i++)
            {
                string frameFile = $"{path}/{i:D3}";
                if (resource.Get(frameFile, usage) == null)
                    break;

                add(frameFile, usage);
            }
        }

        private void collectStageSequence(Action<string, EzTextureUsage> add, string basePath)
        {
            bool any = false;

            for (int i = 0; i < max_frames_to_load; i++)
            {
                string framePath = $"{basePath}_{i}";
                if (resource.Get(framePath, EzTextureUsage.AnimationSafe) == null)
                    break;

                add(framePath, EzTextureUsage.AnimationSafe);
                any = true;
            }

            if (!any)
                collectSingle(add, basePath, EzTextureUsage.Large);
        }

        private void collectKeyedFrames(Action<string, EzTextureUsage> add, string basePath)
        {
            for (int i = 0; i < max_frames_to_load; i++)
            {
                string framePath = $"{basePath}_frame{i}";
                if (resource.Get(framePath, EzTextureUsage.AnimationSafe) == null)
                    break;

                add(framePath, EzTextureUsage.AnimationSafe);
            }
        }

        private void collectSingle(Action<string, EzTextureUsage> add, string path, EzTextureUsage usage)
        {
            if (resource.Get(path, usage) != null)
                add(path, usage);
        }

        #endregion
    }
}
