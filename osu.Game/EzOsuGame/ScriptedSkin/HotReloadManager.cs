// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 热重载管理器，管理脚本皮肤的热重载流程。
    /// </summary>
    public partial class HotReloadManager : Component
    {
        private readonly ScriptFileWatcher fileWatcher;
        private readonly SandboxedScriptRunner scriptRunner;
        private readonly ConcurrentDictionary<string, ReloadStatus> reloadStatuses = new ConcurrentDictionary<string, ReloadStatus>();

        private const string logger_prefix = "[HotReloadManager]";

        /// <summary>
        /// 当皮肤重载完成时触发的事件。
        /// </summary>
        public event Action<string, bool>? SkinReloaded;

        public HotReloadManager(SandboxedScriptRunner runner)
        {
            scriptRunner = runner;
            fileWatcher = new ScriptFileWatcher();
            fileWatcher.ScriptModified += onScriptModified;
        }

        /// <summary>
        /// 开始监控指定目录的脚本文件。
        /// </summary>
        public void StartWatching(string directory)
        {
            fileWatcher.StartWatching(directory);
            Logger.Log($"{logger_prefix} Started watching scripts in: {directory}", LoggingTarget.Information);
        }

        /// <summary>
        /// 停止监控。
        /// </summary>
        public void StopWatching()
        {
            fileWatcher.StopWatching();
        }

        /// <summary>
        /// 手动触发指定脚本的重载。
        /// </summary>
        public async Task<bool> TriggerReload(string scriptPath)
        {
            Logger.Log($"{logger_prefix} Manual reload triggered for: {scriptPath}", LoggingTarget.Information);
            return await reloadScript(scriptPath).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取指定脚本的重载状态。
        /// </summary>
        public ReloadStatus GetReloadStatus(string scriptPath)
        {
            return reloadStatuses.TryGetValue(scriptPath, out var status)
                ? status
                : new ReloadStatus { State = ReloadState.Idle };
        }

        private void onScriptModified(string scriptPath)
        {
            Logger.Log($"{logger_prefix} Auto-reload triggered for: {scriptPath}", LoggingTarget.Information);

            // 使用 Task.Run 在后台线程执行异步操作，避免 async void
            _ = Task.Run(async () =>
            {
                try
                {
                    await reloadScript(scriptPath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"{logger_prefix} Unexpected error in auto-reload: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            });
        }

        private async Task<bool> reloadScript(string scriptPath)
        {
            // 更新状态为重载中
            updateStatus(scriptPath, ReloadState.Reloading, null);

            try
            {
                // 清除旧的编译缓存
                scriptRunner.ClearCache(scriptPath);

                // 重新加载脚本
                var newSkin = await scriptRunner.LoadScriptAsync(scriptPath).ConfigureAwait(false);

                // 更新状态为成功
                updateStatus(scriptPath, ReloadState.Success, null);

                // 触发事件通知
                SkinReloaded?.Invoke(scriptPath, true);

                Logger.Log($"{logger_prefix} Successfully reloaded: {newSkin.Name}", LoggingTarget.Information);
                return true;
            }
            catch (Exception ex)
            {
                // 更新状态为失败
                updateStatus(scriptPath, ReloadState.Failed, ex.Message);

                // 触发事件通知
                SkinReloaded?.Invoke(scriptPath, false);

                Logger.Log($"{logger_prefix} Failed to reload script: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }
        }

        private void updateStatus(string scriptPath, ReloadState state, string? errorMessage)
        {
            var status = new ReloadStatus
            {
                State = state,
                LastReloadTime = DateTime.Now,
                ErrorMessage = errorMessage
            };

            reloadStatuses[scriptPath] = status;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                fileWatcher.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }

    /// <summary>
    /// 重载状态枚举。
    /// </summary>
    public enum ReloadState
    {
        /// <summary>
        /// 空闲状态，未进行重载。
        /// </summary>
        Idle,

        /// <summary>
        /// 正在重载中。
        /// </summary>
        Reloading,

        /// <summary>
        /// 重载成功。
        /// </summary>
        Success,

        /// <summary>
        /// 重载失败。
        /// </summary>
        Failed
    }

    /// <summary>
    /// 重载状态信息。
    /// </summary>
    public class ReloadStatus
    {
        /// <summary>
        /// 当前重载状态。
        /// </summary>
        public ReloadState State { get; set; } = ReloadState.Idle;

        /// <summary>
        /// 最后一次重载的时间。
        /// </summary>
        public DateTime? LastReloadTime { get; set; }

        /// <summary>
        /// 如果重载失败，包含错误信息。
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
