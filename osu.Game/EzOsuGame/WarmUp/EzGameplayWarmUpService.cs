// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.WarmUp
{
    /// <summary>
    /// 自述预热的登记与等待中心。
    /// </summary>
    /// <remarks>
    /// 这里只有「一组 <see cref="IEzGameplayWarmUp"/>」的概念，没有路径知识，也不假设 EzPro：
    /// 组件在自己的 load / LoadComplete 里 <see cref="Register"/>，PlayerLoader 侧等
    /// <see cref="WarmUpAllAsync"/> 完成后放行。
    ///
    /// 注册按会话（session）隔离：进入 <see cref="osu.Game.Screens.Play.PlayerLoader"/> 时 <see cref="BeginSession"/> 清空上一局残留，
    /// 离开时 <see cref="EndSession"/>；选歌 / 编辑器等树里的组件在无会话时登记会被直接忽略，
    /// 因此不会误把 UI 预热算进进图门控。
    /// </remarks>
    public class EzGameplayWarmUpService
    {
        private readonly Lock syncRoot = new Lock();
        private readonly List<IEzGameplayWarmUp> sessionWarmUps = new List<IEzGameplayWarmUp>();
        private bool sessionActive;

        /// <summary>登记序号：每次 <see cref="Register"/> 递增，用于「等完一批后再看有没有新来的」。</summary>
        private int registrationVersion;

        /// <summary>是否处于 <see cref="osu.Game.Screens.Play.PlayerLoader"/> 会话中。</summary>
        public bool IsSessionActive
        {
            get
            {
                lock (syncRoot)
                    return sessionActive;
            }
        }

        /// <summary>开始一次进图预热会话（清空上一局的登记）。</summary>
        public void BeginSession()
        {
            lock (syncRoot)
            {
                sessionWarmUps.Clear();
                registrationVersion++;
                sessionActive = true;
            }
        }

        /// <summary>结束进图预热会话。</summary>
        public void EndSession()
        {
            lock (syncRoot)
            {
                sessionActive = false;
                sessionWarmUps.Clear();
                registrationVersion++;
            }
        }

        /// <summary>登记一个自述预热项（无会话时忽略）。</summary>
        public void Register(IEzGameplayWarmUp? warmUp)
        {
            if (warmUp == null)
                return;

            lock (syncRoot)
            {
                if (!sessionActive)
                    return;

                if (!sessionWarmUps.Contains(warmUp))
                {
                    sessionWarmUps.Add(warmUp);
                    registrationVersion++;
                }
            }
        }

        /// <summary>取消登记一个自述预热项。</summary>
        public void Unregister(IEzGameplayWarmUp? warmUp)
        {
            if (warmUp == null)
                return;

            lock (syncRoot)
                sessionWarmUps.Remove(warmUp);
        }

        /// <summary>
        /// 等待当前已登记的全部自述预热完成。
        /// </summary>
        /// <param name="cancellationToken">离开 PlayerLoader 时会被取消。</param>
        /// <remarks>
        /// 会重复快照直到「等完这一批之后没有新登记」为止：Player 树里的组件是在 Player 加载期间陆续
        /// 把自己登记进来的（HUD 皮肤布局尤其晚），只取一次快照会漏掉后到的那些。
        /// </remarks>
        public async Task WarmUpAllAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                IEzGameplayWarmUp[] snapshot;
                int versionAtSnapshot;

                lock (syncRoot)
                {
                    snapshot = sessionWarmUps.ToArray();
                    versionAtSnapshot = registrationVersion;
                }

                if (snapshot.Length > 0)
                {
                    // 再套一层 WaitAsync：某个实现不理会取消而永远不结束时，也能靠取消让门控退出（而不是干等超时）。
                    await Task.WhenAll(snapshot.Select(w => warmUpOneAsync(w, cancellationToken)))
                              .WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                lock (syncRoot)
                {
                    // 这一批等完之后又有新登记，说明还有组件在加入，继续等。
                    if (registrationVersion == versionAtSnapshot)
                        return;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static async Task warmUpOneAsync(IEzGameplayWarmUp warmUp, CancellationToken cancellationToken)
        {
            try
            {
                await warmUp.WarmUpAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 离开 PlayerLoader 属正常取消。
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzGameplayWarmUpService] Warm-up failed for {warmUp.GetType().Name}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
        }
    }
}
