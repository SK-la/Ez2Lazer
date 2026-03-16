// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame
{
    public partial class EzLocalTextureFactory
    {
        #region 预加载系统

        private static readonly string[] preload_components =
        {
            "whitenote", "bluenote", "greennote",
            "noteflare", "noteflaregood", "longnoteflare",
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

                foreach (string component in preload_components)
                    preloadComponent(component, currentNoteSetName);

                await Task.CompletedTask.ConfigureAwait(false);

                preloadCompleted = true;
                Logger.Log($"[EzLocalTextureFactory] Preload completed for {preload_components.Length} components", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
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

        private void preloadComponent(string component, string noteSetName)
        {
            try
            {
                // 构建纹理路径
                string path = $"note/{noteSetName}/{component}";

                // 预加载动画帧（000.png, 001.png, ...）
                for (int i = 0; i < max_frames_to_load; i++)
                {
                    string frameFile = $"{path}/{i:D3}.png";
                    var texture = textureStore.Get(frameFile);

                    if (texture == null)
                        break; // 没有更多帧了

                    // 触发纹理加载（不保存，只让 TextureStore 缓存）
                    Logger.Log($"[EzLocalTextureFactory] Preloaded: {frameFile}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                }

                // 额外预加载带颜色的变体（如果需要）
                if (component.StartsWith("white", StringComparison.Ordinal))
                {
                    string blueComponent = component.Replace("white", "blue");
                    string greenComponent = component.Replace("white", "green");

                    preloadColorVariant(blueComponent, noteSetName);
                    preloadColorVariant(greenComponent, noteSetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Failed to preload {component}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
        }

        private void preloadColorVariant(string component, string noteSetName)
        {
            try
            {
                string path = $"note/{noteSetName}/{component}";

                for (int i = 0; i < max_frames_to_load; i++)
                {
                    string frameFile = $"{path}/{i:D3}.png";
                    var texture = textureStore.Get(frameFile);

                    if (texture == null)
                        break;

                    Logger.Log($"[EzLocalTextureFactory] Preloaded color variant: {frameFile}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Failed to preload color variant {component}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
        }

        private void resetPreloadState()
        {
            preloadCompleted = false;
            isPreloading = false;
        }

        #endregion
    }
}
