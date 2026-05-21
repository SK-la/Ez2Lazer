// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 脚本编译缓存，避免重复编译相同的脚本文件以提升性能。
    /// </summary>
    /// <remarks>
    /// 此缓存基于文件内容的 SHA256 哈希值作为键，确保只有在脚本内容真正改变时才重新编译。
    /// 缓存会在内存中保存已编译的脚本实例，直到被显式清除或对象销毁。
    /// </remarks>
    public class ScriptCompilationCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, CachedEntry> cache = new ConcurrentDictionary<string, CachedEntry>();
        private static readonly Logger logger = Logger.GetLogger("ScriptCompilationCache");

        /// <summary>
        /// 获取缓存中的条目数量。
        /// </summary>
        public int Count => cache.Count;

        /// <summary>
        /// 尝试从缓存中获取已编译的脚本实例。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <param name="skin">如果缓存命中，返回编译后的皮肤实例；否则为 null。</param>
        /// <returns>如果缓存命中返回 true，否则返回 false。</returns>
        public bool TryGet(string scriptPath, out IScriptedSkin? skin)
        {
            if (!File.Exists(scriptPath))
            {
                skin = null;
                return false;
            }

            string cacheKey = computeCacheKey(scriptPath);

            if (cache.TryGetValue(cacheKey, out var entry))
            {
                // 检查文件是否已修改
                var lastWriteTime = File.GetLastWriteTimeUtc(scriptPath);

                if (entry.LastModified == lastWriteTime)
                {
                    Logger.Log($"Cache hit for {Path.GetFileName(scriptPath)}", LoggingTarget.Information);
                    skin = entry.Skin;
                    return true;
                }

                // 文件已修改，移除旧缓存
                Logger.Log($"Cache invalidated for {Path.GetFileName(scriptPath)} (file modified)", LoggingTarget.Information);
                RemoveByPath(scriptPath);
            }

            skin = null;
            return false;
        }

        /// <summary>
        /// 将编译后的脚本实例添加到缓存中。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <param name="skin">编译后的皮肤实例。</param>
        public void Add(string scriptPath, IScriptedSkin skin)
        {
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Script file not found: {scriptPath}");

            string cacheKey = computeCacheKey(scriptPath);
            var lastWriteTime = File.GetLastWriteTimeUtc(scriptPath);

            // 如果已存在相同键的缓存，先清理旧实例
            if (cache.TryGetValue(cacheKey, out var oldEntry))
            {
                oldEntry.Skin?.Dispose();
            }

            cache[cacheKey] = new CachedEntry(skin, lastWriteTime, scriptPath);
            Logger.Log($"Cached compiled script: {Path.GetFileName(scriptPath)} (Total: {cache.Count})", LoggingTarget.Information);
        }

        /// <summary>
        /// 根据脚本路径移除缓存条目。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <returns>如果成功移除返回 true，否则返回 false。</returns>
        public bool RemoveByPath(string scriptPath)
        {
            string cacheKey = computeCacheKey(scriptPath);

            if (cache.TryRemove(cacheKey, out var entry))
            {
                entry.Skin?.Dispose();
                Logger.Log($"Removed from cache: {Path.GetFileName(scriptPath)}", LoggingTarget.Information);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清除所有缓存条目并释放相关资源。
        /// </summary>
        public void Clear()
        {
            Logger.Log($"Clearing all cached scripts ({cache.Count} entries)", LoggingTarget.Information);

            foreach (var entry in cache.Values)
            {
                entry.Skin?.Dispose();
            }

            cache.Clear();
        }

        /// <summary>
        /// 计算脚本文件的缓存键（基于文件路径和内容哈希）。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <returns>缓存键字符串。</returns>
        private static string computeCacheKey(string scriptPath)
        {
            // 使用文件路径作为主要标识
            string fullPath = Path.GetFullPath(scriptPath);

            // 可选：添加文件内容哈希以增强准确性
            // 这样即使文件被替换为同名但内容不同的文件也能正确识别
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(fullPath);
                byte[] hash = sha256.ComputeHash(stream);
                string hashString = Convert.ToBase64String(hash);

                // 结合路径和哈希生成唯一键
                return $"{fullPath}|{hashString}";
            }
            catch
            {
                // 如果无法计算哈希，仅使用路径
                return fullPath;
            }
        }

        /// <summary>
        /// 释放缓存占用的资源。
        /// </summary>
        public void Dispose()
        {
            Clear();
        }

        /// <summary>
        /// 缓存条目，包含编译后的皮肤实例和元数据。
        /// </summary>
        private record CachedEntry(IScriptedSkin Skin, DateTime LastModified, string SourcePath);
    }
}
