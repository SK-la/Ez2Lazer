// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 脚本化皮肤的统一接口，用户编写的 .csx 文件必须实现此接口。
    /// </summary>
    /// <remarks>
    /// 此接口定义了脚本皮肤需要实现的核心方法，允许用户通过 C# 脚本自定义皮肤行为。
    /// 脚本将在沙箱环境中执行，以确保安全性。
    /// </remarks>
    public interface IScriptedSkin : IDisposable
    {
        /// <summary>
        /// 皮肤初始化（在加载时调用一次）。
        /// </summary>
        /// <param name="baseSkin">基础皮肤源，可用于回退查找。</param>
        /// <param name="resources">资源提供者，用于访问游戏资源。</param>
        void Initialize(ISkinSource baseSkin, IStorageResourceProvider resources);

        /// <summary>
        /// 获取 Drawable 组件（核心方法）。
        /// </summary>
        /// <param name="lookup">组件查找条件。</param>
        /// <returns>Drawable 组件实例，或 null 表示使用默认实现。</returns>
        Drawable? GetDrawableComponent(ISkinComponentLookup lookup);

        /// <summary>
        /// 获取纹理。
        /// </summary>
        /// <param name="componentName">纹理名称。</param>
        /// <param name="wrapModeS">水平方向的纹理包裹模式。</param>
        /// <param name="wrapModeT">垂直方向的纹理包裹模式。</param>
        /// <returns>纹理实例，或 null 表示未找到。</returns>
        Texture? GetTexture(string componentName, WrapMode wrapModeS = default, WrapMode wrapModeT = default);

        /// <summary>
        /// 获取音效。
        /// </summary>
        /// <param name="sampleInfo">音效信息。</param>
        /// <returns>音效实例，或 null 表示未找到。</returns>
        ISample? GetSample(ISampleInfo sampleInfo);

        /// <summary>
        /// 获取配置值。
        /// </summary>
        /// <typeparam name="TLookup">查找类型。</typeparam>
        /// <typeparam name="TValue">值类型。</typeparam>
        /// <param name="lookup">配置查找条件。</param>
        /// <returns>配置值的 Bindable 包装，或 null 表示未找到。</returns>
        IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull;

        /// <summary>
        /// 皮肤名称（用于在 UI 中显示）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 作者信息。
        /// </summary>
        string Author { get; }

        /// <summary>
        /// 版本号。
        /// </summary>
        Version Version { get; }
    }

    /// <summary>
    /// 可选的脚本皮肤元数据接口。
    /// </summary>
    /// <remarks>
    /// 仅当脚本需要参与受保护皮肤、导入/编辑策略时实现。
    /// 不实现时默认视为未保护，保持旧脚本兼容。
    /// </remarks>
    public interface IScriptedSkinMetadata
    {
        /// <summary>
        /// 脚本皮肤是否受保护。
        /// </summary>
        bool Protected { get; }
    }
}
