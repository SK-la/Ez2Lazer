// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;

namespace osu.Desktop
{
    /// <summary>
    /// GC 形态只能在 runtime 初始化时确定，且晚于进入 <see cref="Program.Main"/>；
    /// 因此本类型在启动最早期读取用户选择，若与当前进程不符，则以环境变量重入一个新进程并让当前进程退出。
    ///
    /// 环境变量 <c>DOTNET_gcServer</c> 在 .NET 9+ 优先级高于 runtimeconfig.json，故无需改动安装目录文件
    /// （runtimeconfig 仍声明 Server GC 作为默认值）。哨兵环境变量防止无限重入。
    /// </summary>
    internal static class EzGcModeBootstrap
    {
        private const string bootstrapped_env = "EZ2LAZER_GC_BOOTSTRAPPED";

        private const string gc_mode_key = "GcMode";

        /// <summary>
        /// 是否需要重启进程以应用 GC 模式。返回 true 时调用方应立即退出。
        /// </summary>
        public static bool EnsureRequestedGcMode(string gameName, string[] args)
        {
            // 只在首次进入时判断；重入的子进程已带上正确环境变量。
            if (Environment.GetEnvironmentVariable(bootstrapped_env) == "1")
                return false;

            // 带参数启动（导入文件 / tournament / velopack 钩子）时不做 GC 重入，避免干扰这些流程。
            if (args.Length > 0)
                return false;

            // 附加调试器时不重入，否则会丢掉调试会话。
            if (Debugger.IsAttached)
                return false;

            var requested = readRequestedMode(gameName);

            // 配置缺失按默认（Server）处理，与 Ez2ConfigManager.SetDefault 保持一致。
            bool wantServer = requested != EzGcMode.Workstation;

            if (wantServer == GCSettings.IsServerGC)
                return false;

            if (!tryRelaunch(wantServer, args))
                return false;

            return true;
        }

        private static bool tryRelaunch(bool serverGc, string[] args)
        {
            string? processPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(processPath))
                return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory,
                };

                foreach (string arg in args)
                    startInfo.ArgumentList.Add(arg);

                startInfo.Environment[bootstrapped_env] = "1";
                startInfo.Environment["DOTNET_gcServer"] = serverGc ? "1" : "0";

                Process.Start(startInfo);
                return true;
            }
            catch
            {
                // 重入失败时按当前进程继续，避免因 GC 偏好无法启动游戏。
                return false;
            }
        }

        /// <summary>
        /// 读取 <c>GcMode</c>。数据目录可由 <c>storage.ini</c> 重定向，故先解析存储根目录再读配置。
        /// 任何读取失败都回退为 null（= 默认 Server GC）。
        /// </summary>
        private static EzGcMode? readRequestedMode(string gameName)
        {
            try
            {
                string? dataDirectory = resolveDataDirectory(gameName);

                if (dataDirectory == null)
                    return null;

                string configPath = Path.Combine(dataDirectory, "EzSkinSettings.ini");

                if (!File.Exists(configPath))
                    return null;

                foreach (string line in File.ReadLines(configPath))
                {
                    int separator = line.IndexOf('=');

                    if (separator < 0)
                        continue;

                    if (!line.AsSpan(0, separator).Trim().Equals(gc_mode_key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string value = line.AsSpan(separator + 1).Trim().ToString();

                    return Enum.TryParse(value, true, out EzGcMode mode) ? mode : null;
                }
            }
            catch
            {
                // 配置不可读时按默认处理。
            }

            return null;
        }

        /// <summary>
        /// 解析实际数据目录：默认在漫游 AppData 下，但若 <c>storage.ini</c> 指定了自定义路径则用该路径。
        /// </summary>
        private static string? resolveDataDirectory(string gameName)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrEmpty(roaming))
                return null;

            string defaultDirectory = Path.Combine(roaming, gameName);
            string storageIni = Path.Combine(defaultDirectory, "storage.ini");

            if (!File.Exists(storageIni))
                return defaultDirectory;

            foreach (string line in File.ReadLines(storageIni))
            {
                int separator = line.IndexOf('=');

                if (separator < 0)
                    continue;

                if (!line.AsSpan(0, separator).Trim().Equals("FullPath", StringComparison.Ordinal))
                    continue;

                string value = line.AsSpan(separator + 1).Trim().ToString();

                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                    return value;
            }

            return defaultDirectory;
        }
    }

    /// <summary>
    /// 与 <c>osu.Game.EzOsuGame.Configuration.EzGcMode</c> 对应的启动期副本。
    /// 启动引导在 osu.Game 程序集加载前运行，不能引用其中的类型，故此处按名字解析。
    /// </summary>
    internal enum EzGcMode
    {
        Server,
        Workstation,
    }
}
