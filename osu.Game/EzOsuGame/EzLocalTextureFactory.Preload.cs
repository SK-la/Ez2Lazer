// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame
{
    public partial class EzLocalTextureFactory
    {
        #region 预加载系统

        private static readonly string[] preload_components =
        {
            "whitenote", "noteflare", "noteflaregood", "longnoteflare",
            "longnote/body", "longnote/head", "longnote/tail"
        };

        private static volatile bool isPreloading;
        private static volatile bool preloadCompleted;

        public async Task PreloadGameTextures()
        {
            if (preloadCompleted || isPreloading) return;

            isPreloading = true;

            try
            {
                string currentNoteSetName = noteSetName.Value;
                Logger.Log($"[EzLocalTextureFactory] Starting preload for note set: {currentNoteSetName}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                // 收集要强制加载的纹理路径（保持现有逻辑但不触发重复上传）
                var allFramePaths = new List<string>();

                foreach (string component in preload_components)
                {
                    try
                    {
                        string path = $"note/{currentNoteSetName}/{component}";

                        for (int i = 0; i < max_frames_to_load; i++)
                        {
                            string frameFile = $"{path}/{i:D3}";
                            var texture = textureStore.Get(frameFile);

                            if (texture == null)
                                break;

                            allFramePaths.Add(frameFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[EzLocalTextureFactory] Failed to collect frames for {component}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                    }
                }

                // 如果没有找到任何纹理，直接结束
                if (allFramePaths.Count == 0)
                {
                    preloadCompleted = true;
                    Logger.Log($"[EzLocalTextureFactory] Nothing to preload for note set: {currentNoteSetName}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    return;
                }

                // 在主线程中创建一个临时 Holder 并把要预热的 Sprite 通过 LoadComponentAsync 加载进 Holder，从而强制上传纹理到 GPU。
                var loadTcs = new TaskCompletionSource<bool>();

                Schedule(() =>
                {
                    var holder = new Container { Alpha = 0, RelativeSizeAxes = Framework.Graphics.Axes.Both };
                    AddInternal(holder);

                    var loadTasks = new List<Task>();

                    foreach (string framePath in allFramePaths)
                    {
                        try
                        {
                            var texture = textureStore.Get(framePath);
                            if (texture == null) continue;

                            var sprite = new Sprite { Texture = texture, Alpha = 0 };

                            var tcs = new TaskCompletionSource<bool>();
                            loadTasks.Add(tcs.Task);

                            // 异步加载 Sprite，并在加载完成后加入 holder。这样会触发框架在加载阶段把纹理上传到 GPU。
                            LoadComponentAsync(sprite, s =>
                            {
                                holder.Add(s);
                                // 延迟一个周期确保上传开始/完成
                                Scheduler.AddOnce(() => tcs.SetResult(true));
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[EzLocalTextureFactory] Error scheduling preload for {framePath}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                        }
                    }

                    // 等待所有加载任务完成，然后清理 holder 并标记完成
                    Task.WhenAll(loadTasks).ContinueWith(_ =>
                    {
                        Schedule(() =>
                        {
                            holder.RemoveAll(d => true, true);
                            RemoveInternal(holder, true);
                            loadTcs.TrySetResult(true);
                        });
                    }, TaskScheduler.Default);
                });

                // 等待主线程上的加载流程完成
                await loadTcs.Task.ConfigureAwait(false);

                preloadCompleted = true;
                Logger.Log($"[EzLocalTextureFactory] Preload completed for {allFramePaths.Count} frames", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Preload failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
            finally
            {
                isPreloading = false;
            }
        }

        #endregion
    }
}
