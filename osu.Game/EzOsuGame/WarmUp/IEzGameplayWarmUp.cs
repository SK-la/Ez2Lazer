// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.WarmUp
{
    /// <summary>
    /// 「在进局前需要完成的工作」。
    /// </summary>
    /// <remarks>
    /// 由组件在自己的 load / LoadComplete 里登记到 <see cref="EzGameplayWarmUpService"/>，
    /// PlayerLoader 会等全部自述预热完成后才放行进局。
    /// 这里没有任何中心化路径表，也不假设具体皮肤——谁用谁负责。
    /// </remarks>
    public interface IEzGameplayWarmUp
    {
        /// <summary>
        /// 完成本组件进局所需的纹理探测 / 解码。
        /// </summary>
        /// <param name="cancellationToken">离开 PlayerLoader 时会被取消。</param>
        Task WarmUpAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 规则集侧的自述预热入口：按当前皮肤产出 gameplay 组件（离屏加载）以触发纹理解码。
    /// </summary>
    /// <remarks>
    /// 由规则集实现（见 <c>ManiaRuleset</c>）。调用方（<c>EzPlayerLoaderStartGate</c>）
    /// 会在 <see cref="osu.Game.Screens.Play.PlayerLoader"/> 进入时请求一个独立的预热 drawable，离屏加载并更新数帧后销毁，
    /// 从而把当前皮肤 gameplay 组件的纹理创建提前到转圈期间、且离开 update 线程。
    /// <para>
    /// 传的是 <see cref="IBeatmapInfo"/> 而不是转换后的谱面：预热只关心「布局尺度与进局一致」，
    /// 而元数据已经在内存里，取键数是零成本；为预热去解码 / 转换谱面，代价远大于它省下的那点解码。
    /// 真的需要内容的规则集应自行承担该成本，不要把这个成本摊到所有规则集的进图路径上。
    /// </para>
    /// </remarks>
    public interface IEzGameplayWarmUpSource
    {
        /// <summary>
        /// 返回一个独立的 drawable，其加载过程会完成当前皮肤 gameplay 组件的纹理解码。
        /// </summary>
        /// <param name="skin">当前皮肤。</param>
        /// <param name="beatmapInfo">当前谱面元数据（不应因此加载或转换谱面内容）。</param>
        /// <param name="mods">本局 mods；影响布局尺度的 mod（例如 mania 的 key mod）需要它。</param>
        /// <returns>没有需要预热的内容时返回 <c>null</c>。</returns>
        Drawable? CreateWarmUpDrawable(ISkin skin, IBeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods);
    }
}
