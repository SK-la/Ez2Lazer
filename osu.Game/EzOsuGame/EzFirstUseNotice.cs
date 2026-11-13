// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 记住某个功能说明是否已经对这台数据目录弹出过。键是稳定字符串，存在 <c>EzResources/first-use.keys</c>。
    /// </summary>
    public static class EzFirstUseNotice
    {
        public const string legacy_skin_storage = "legacy-skin-storage";

        private static readonly object gate = new object();

        public static bool HasSeen(Storage storage, string key)
        {
            try
            {
                lock (gate)
                    return readKeys(storage).Contains(key);
            }
            catch (Exception e)
            {
                Logger.Error(e, "无法读取首次说明记录。");
                return false;
            }
        }

        public static void MarkSeen(Storage storage, string key)
        {
            try
            {
                lock (gate)
                {
                    HashSet<string> keys = readKeys(storage);

                    if (!keys.Add(key))
                        return;

                    string path = keysPath(storage);
                    string? parent = Path.GetDirectoryName(path);

                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    File.WriteAllLines(path, keys);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "无法记下首次说明已读。");
            }
        }

        private static HashSet<string> readKeys(Storage storage)
        {
            string path = keysPath(storage);

            if (!File.Exists(path))
                return new HashSet<string>(StringComparer.Ordinal);

            return new HashSet<string>(File.ReadAllLines(path), StringComparer.Ordinal);
        }

        private static string keysPath(Storage storage) => storage.GetFullPath($"{EzModifyPath.RESOURCES_PATH}/first-use.keys");
    }
}
