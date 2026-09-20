// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Scoring;

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

        /// <summary>判定动画可能用到的全部结果名（各 HitMode 模板的并集，见 <see cref="EzHitResultNameTemplate"/>）。</summary>
        private static readonly HitResult[] judgement_result_names =
        {
            HitResult.Perfect,
            HitResult.Great,
            HitResult.Good,
            HitResult.Ok,
            HitResult.Meh,
            HitResult.Miss,
            HitResult.Poor,
        };

        private const int max_judgement_frames = 64;

        private static readonly string[] judgement_frame_separators = { "-", "_" };

        /// <summary>帧序号补零宽度（<c>frame_0</c> / <c>frame_00</c> / <c>frame_000</c>）。</summary>
        private static readonly string[] judgement_frame_number_formats = { "D1", "D2", "D3" };

        private volatile bool isPreloading;
        private volatile bool preloadCompleted;
        private string? completedPreloadKey;
        private Task? preloadTask;

        /// <summary>
        /// 当前 NoteSet/Stage/GameTheme 是否已完成解码缓存预热。
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
            => $"{noteSetName.Value}|{stageName.Value}|{gameThemeName.Value}";

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

            collectJudgementTextures(add);

            return frames;
        }

        /// <summary>
        /// 预热 <c>EzHUDHitResultScore</c> 的判定动画与全连演出帧。
        /// </summary>
        /// <remarks>
        /// 这些资源位于用户 <c>EzResources/GameTheme/{theme}/judgement/</c> 与 <c>EzResources/FullCombo/</c>，
        /// 既不在 note/stage 集合里、也没有其它预热入口，因此在**首次**出现该判定时才由 HUD 解码
        /// （磁盘读 + PNG 解码 + 首次上传），表现为进游戏后第一个 note 判定时 update/draw 各掉一帧。
        /// 这里按结果名逐一探测，把解码提前到 PlayerLoader 期间的线程池里。
        /// </remarks>
        private void collectJudgementTextures(Action<string, EzTextureUsage> add)
        {
            string theme = gameThemeName.Value.ToString();
            string judgementRoot = $"GameTheme/{theme}/judgement";

            foreach (string resultName in enumerateJudgementResourceNames())
            {
                string basePath = $"{judgementRoot}/{resultName}";

                collectSeparatedFrames(add, basePath);
                collectTemplateFrames(add, basePath);
            }

            // checkFullCombo 直接取 FullCombo/full-combo（不带 GameTheme 前缀），帧名走 -/_ 分隔符。
            collectSeparatedFrames(add, "FullCombo/full-combo");
        }

        /// <summary>
        /// 枚举判定资源名：各 <see cref="EzEnumHitMode"/> 模板的结果名并集，且补齐 HUD 会尝试的大小写变体。
        /// </summary>
        private static IEnumerable<string> enumerateJudgementResourceNames()
        {
            var yielded = new HashSet<string>(StringComparer.Ordinal);

            foreach (EzEnumHitMode mode in Enum.GetValues<EzEnumHitMode>())
            {
                foreach (HitResult result in judgement_result_names)
                {
                    string name = EzHitResultNameTemplate.GetResourceName(mode, result);

                    if (string.IsNullOrEmpty(name))
                        continue;

                    // CreateJudgementTexture 依次尝试原名 / 小写 / 大写。
                    if (yielded.Add(name))
                        yield return name;
                    if (yielded.Add(name.ToLowerInvariant()))
                        yield return name.ToLowerInvariant();
                    if (yielded.Add(name.ToUpperInvariant()))
                        yield return name.ToUpperInvariant();
                }
            }
        }

        /// <summary>
        /// 判定帧有两种命名约定：直接接基名（<c>Kool-0</c> / <c>Kool_0</c>），以及默认帧模板（<c>Kool/frame_0</c>）。
        /// 两种都探测；不存在的名字在首个帧探测即中断，代价是一次已缓存为 null 的查找。
        /// 自定义帧模板（如 <c>frame_{000}</c>）无法在此预知，仍留给首次判定时解码。
        /// </summary>
        private void collectSeparatedFrames(Action<string, EzTextureUsage> add, string basePath)
        {
            foreach (string separator in judgement_frame_separators)
            {
                for (int i = 0; i < max_judgement_frames; i++)
                {
                    string path = $"{basePath}{separator}{i}";

                    if (resource.Get(path, EzTextureUsage.AnimationSafe) == null)
                        break;

                    add(path, EzTextureUsage.AnimationSafe);
                }
            }
        }

        private void collectTemplateFrames(Action<string, EzTextureUsage> add, string basePath)
        {
            // 默认模板 {result}/frame_{0}；{00}/{000} 是 formatJudgementFrameTemplate 同样支持的补零宽度。
            foreach (string numberFormat in judgement_frame_number_formats)
            {
                for (int i = 0; i < max_judgement_frames; i++)
                {
                    string path = $"{basePath}/frame_{i.ToString(numberFormat, CultureInfo.InvariantCulture)}";

                    if (resource.Get(path, EzTextureUsage.AnimationSafe) == null)
                        break;

                    add(path, EzTextureUsage.AnimationSafe);
                }
            }
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
