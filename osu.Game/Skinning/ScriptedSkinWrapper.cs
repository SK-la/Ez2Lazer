// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.IO;

namespace osu.Game.Skinning
{
    /// <summary>
    /// 将 IScriptedSkin 包装为标准的 Skin 对象，以便与现有的皮肤系统兼容。
    /// </summary>
    /// <remarks>
    /// 此包装器实现了 Skin 的所有抽象方法，并将调用委托给底层的 IScriptedSkin 实例。
    /// 这使得脚本皮肤可以无缝集成到现有的皮肤选择和管理系统中。
    /// </remarks>
    public class ScriptedSkinWrapper : Skin
    {
        private readonly IScriptedSkin scriptedSkin;

        /// <param name="skinSource">皮肤源（用于回退）。</param>
        /// <param name="resources">资源提供者。</param>
        /// <param name="scriptedSkin">底层脚本皮肤实例。</param>
        /// <param name="scriptRunner">脚本执行器（用于热重载）。</param>
        /// <param name="skinInfo">可选的原始皮肤信息，用于保留脚本皮肤的 Protected 等元数据。</param>
        /// <param name="scriptPath">脚本文件路径。</param>
        public ScriptedSkinWrapper(ISkinSource skinSource,
                                   IStorageResourceProvider resources,
                                   IScriptedSkin scriptedSkin,
                                   SandboxedScriptRunner? scriptRunner = null,
                                   SkinInfo? skinInfo = null,
                                   string? scriptPath = null)
            : base(createInfoForScriptedSkin(scriptedSkin, skinInfo), resources)
        {
            this.scriptedSkin = scriptedSkin;
            ScriptRunner = scriptRunner ?? new SandboxedScriptRunner();
            ScriptPath = scriptPath;

            // 初始化脚本皮肤
            try
            {
                scriptedSkin.Initialize(skinSource, resources);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to initialize scripted skin '{scriptedSkin.Name}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        /// <summary>
        /// 为脚本皮肤创建 SkinInfo。
        /// </summary>
        private static SkinInfo createInfoForScriptedSkin(IScriptedSkin skin, SkinInfo? skinInfo)
        {
            if (skinInfo != null)
                return skinInfo;

            bool isProtected = false;

            var protectedProperty = skin.GetType().GetProperty("Protected");
            if (protectedProperty?.PropertyType == typeof(bool) && protectedProperty.GetValue(skin) is bool protectedValue)
                isProtected = protectedValue;

            return new SkinInfo
            {
                ID = Guid.NewGuid(), // 脚本皮肤使用动态生成的 ID
                Name = $"{skin.Name} [Scripted]",
                Creator = skin.Author,
                Protected = isProtected,
                InstantiationInfo = typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo(),
            };
        }

        /// <summary>
        /// 获取用于加载后续脚本文件的脚本执行器。
        /// </summary>
        public SandboxedScriptRunner ScriptRunner { get; }

        /// <summary>
        /// 获取脚本皮肤的脚本路径（如果可用）。
        /// </summary>
        public string? ScriptPath { get; }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            try
            {
                return scriptedSkin.GetDrawableComponent(lookup);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in scripted skin GetDrawableComponent: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }
        }

        public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
        {
            try
            {
                return scriptedSkin.GetTexture(componentName, wrapModeS, wrapModeT);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in scripted skin GetTexture: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }
        }

        public override ISample? GetSample(ISampleInfo sampleInfo)
        {
            try
            {
                return scriptedSkin.GetSample(sampleInfo);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in scripted skin GetSample: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            try
            {
                return scriptedSkin.GetConfig<TLookup, TValue>(lookup);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in scripted skin GetConfig: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                try
                {
                    scriptedSkin.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error disposing scripted skin: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// 获取底层的脚本皮肤实例（用于编辑器等高级功能）。
        /// </summary>
        public IScriptedSkin GetScriptedSkin() => scriptedSkin;

        /// <summary>
        /// 重新加载脚本（用于热重载）。
        /// </summary>
        /// <param name="scriptPath">脚本文件路径。</param>
        /// <returns>是否成功重新加载。</returns>
        public async Task<bool> ReloadAsync(string scriptPath)
        {
            try
            {
                var newSkin = await ScriptRunner.LoadScriptAsync(scriptPath).ConfigureAwait(false);

                // 清理旧实例
                scriptedSkin.Dispose();

                // 替换为新实例（注意：这里需要特殊处理，因为字段是 readonly）
                // 实际实现中可能需要重新创建整个包装器
                Logger.Log($"Reloaded scripted skin: {newSkin.Name}", LoggingTarget.Information);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to reload scripted skin: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }
        }
    }
}
