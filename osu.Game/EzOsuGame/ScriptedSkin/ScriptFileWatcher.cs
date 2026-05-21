// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Timers;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 脚本文件监控器，监听指定目录下的 .csx 文件变化。
    /// </summary>
    /// <remarks>
    /// 使用防抖机制避免频繁触发重载事件。
    /// 当文件被修改、创建或删除时，会延迟一段时间后触发事件。
    /// </remarks>
    public class ScriptFileWatcher : IDisposable
    {
        private FileSystemWatcher? watcher;
        private Timer? debounceTimer;
        private string? pendingFilePath;
        private const double debounce_delay_ms = 500; // 防抖延迟 500ms

        private const string logger_prefix = "[ScriptFileWatcher]";

        /// <summary>
        /// 当脚本文件被修改时触发的事件。
        /// </summary>
        public event Action<string>? ScriptModified;

        /// <summary>
        /// 开始监控指定目录下的脚本文件。
        /// </summary>
        /// <param name="directory">要监控的目录路径。</param>
        public void StartWatching(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Logger.Log($"{logger_prefix} Directory does not exist: {directory}", LoggingTarget.Information);
                return;
            }

            StopWatching();

            watcher = new FileSystemWatcher(directory)
            {
                Filter = "*.csx",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            watcher.Changed += onFileChanged;
            watcher.Created += onFileChanged;
            watcher.Deleted += onFileChanged;
            watcher.Renamed += onFileRenamed;

            watcher.EnableRaisingEvents = true;

            Logger.Log($"{logger_prefix} Started watching directory: {directory}", LoggingTarget.Information);
        }

        /// <summary>
        /// 停止监控。
        /// </summary>
        public void StopWatching()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;

                Logger.Log($"{logger_prefix} Stopped watching scripts", LoggingTarget.Information);
            }

            stopDebounceTimer();
        }

        private void onFileChanged(object sender, FileSystemEventArgs e)
        {
            Logger.Log($"{logger_prefix} File changed: {e.FullPath} ({e.ChangeType})", LoggingTarget.Information);
            triggerDebounce(e.FullPath);
        }

        private void onFileRenamed(object sender, RenamedEventArgs e)
        {
            Logger.Log($"{logger_prefix} File renamed: {e.OldFullPath} -> {e.FullPath}", LoggingTarget.Information);

            // 旧文件删除，触发一次
            triggerDebounce(e.OldFullPath);

            // 新文件创建，触发一次
            triggerDebounce(e.FullPath);
        }

        private void triggerDebounce(string filePath)
        {
            // 只关注 .csx 文件
            if (!filePath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
                return;

            pendingFilePath = filePath;

            // 重置防抖定时器
            stopDebounceTimer();
            debounceTimer = new Timer(debounce_delay_ms);
            debounceTimer.Elapsed += onDebounceElapsed;
            debounceTimer.AutoReset = false;
            debounceTimer.Start();
        }

        private void onDebounceElapsed(object? sender, ElapsedEventArgs e)
        {
            if (pendingFilePath != null)
            {
                Logger.Log($"{logger_prefix} Triggering reload for: {Path.GetFileName(pendingFilePath)}", LoggingTarget.Information);

                // 在主线程触发事件（如果需要）
                ScriptModified?.Invoke(pendingFilePath);

                pendingFilePath = null;
            }

            stopDebounceTimer();
        }

        private void stopDebounceTimer()
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
                debounceTimer = null;
            }
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
