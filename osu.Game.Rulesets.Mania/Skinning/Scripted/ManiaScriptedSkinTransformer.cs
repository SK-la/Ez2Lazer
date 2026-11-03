// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Scripted
{
    /// <summary>
    /// Mania 规则集的脚本皮肤 Transformer。
    /// 自动检测并加载 ManiaTransformer.csx（如果存在）。
    /// </summary>
    public class ManiaScriptedSkinTransformer : SkinTransformer
    {
        private readonly ISkin sourceSkin;
        private readonly ScriptedSkinWrapper? scriptedWrapper;
        private readonly IBeatmap beatmap;
        private SkinTransformer? maniaTransformer;
        private bool maniaTransformerLoadAttempted;

        public ManiaScriptedSkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin)
        {
            sourceSkin = skin;
            this.beatmap = beatmap;
            scriptedWrapper = skin as ScriptedSkinWrapper;
        }

        private SkinTransformer? getManiaTransformer()
        {
            if (maniaTransformerLoadAttempted)
                return maniaTransformer;

            maniaTransformerLoadAttempted = true;

            if (scriptedWrapper == null)
                return null;

            maniaTransformer = loadManiaTransformer(scriptedWrapper);
            return maniaTransformer;
        }

        /// <summary>
        /// 加载 Mania 专用的 transformer 脚本
        /// </summary>
        private SkinTransformer? loadManiaTransformer(ScriptedSkinWrapper wrapper)
        {
            try
            {
                string? scriptPath = wrapper.ScriptPath ?? SkinManager.GetScriptPathStatic(wrapper.SkinInfo.Value);
                if (string.IsNullOrEmpty(scriptPath))
                    return null;

                string scriptDirectory = Path.GetDirectoryName(scriptPath)!;
                string skinStem = Path.GetFileNameWithoutExtension(scriptPath);
                string[] candidatePaths =
                {
                    Path.Combine(scriptDirectory, $"Mania{skinStem}Transformer.csx"),
                    Path.Combine(scriptDirectory, "ManiaTransformer.csx"),
                };

                string maniaTransformerPath = candidatePaths.FirstOrDefault(File.Exists) ?? string.Empty;

                if (string.IsNullOrEmpty(maniaTransformerPath))
                    return null;

                Logger.Log($"发现 Mania Transformer: {Path.GetFileName(maniaTransformerPath)}", LoggingTarget.Information);
                return Task.Run(() => wrapper.ScriptRunner.LoadScriptAsync<SkinTransformer>(maniaTransformerPath, sourceSkin, beatmap)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Log($"加载 Mania Transformer 失败: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            var transformer = getManiaTransformer();

            // 1️⃣ 如果有 Mania transformer，先尝试从它获取配置
            var transformerConfig = transformer?.GetConfig<TLookup, TValue>(lookup);
            if (transformerConfig != null)
                return transformerConfig;

            // 2️⃣ 尝试从基础脚本皮肤获取配置
            var scriptConfig = base.GetConfig<TLookup, TValue>(lookup);
            if (scriptConfig != null)
                return scriptConfig;

            // 3️⃣ 如果都没提供，使用 Mania 默认配置
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnWidth:
                        return SkinUtils.As<TValue>(new Bindable<float>(64));

                    case LegacyManiaSkinConfigurationLookups.HitPosition:
                        return SkinUtils.As<TValue>(new Bindable<float>(430));

                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:
                        return SkinUtils.As<TValue>(new Bindable<Color4>(Color4.Black));
                }
            }

            return null;
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            var transformer = getManiaTransformer();

            // 1️⃣ 如果有 Mania transformer，先尝试从它获取组件
            var transformerComponent = transformer?.GetDrawableComponent(lookup);
            if (transformerComponent != null)
                return transformerComponent;

            // 2️⃣ 回退到基础脚本皮肤
            return base.GetDrawableComponent(lookup);
        }
    }
}
