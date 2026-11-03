// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// Helpers for detecting scripted skins and resolving on-disk script directories.
    /// </summary>
    public static class ScriptedSkinSupport
    {
        /// <summary>
        /// Whether <paramref name="skin"/> is a scripted skin entry (by instantiation type).
        /// </summary>
        public static bool IsScriptedSkin(SkinInfo skin) =>
            skin.InstantiationInfo == typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo();

        /// <summary>
        /// Whether <paramref name="skinInfo"/> refers to a scripted skin.
        /// </summary>
        public static bool IsScriptedSkin(Live<SkinInfo> skinInfo) => skinInfo.PerformRead(IsScriptedSkin);

        /// <summary>
        /// Scripted and other unmanaged skins must not use Realm external-edit import.
        /// </summary>
        public static bool CanUseRealmExternalEdit(SkinInfo skin, bool isManaged) => isManaged && !IsScriptedSkin(skin);

        /// <summary>
        /// Whether <paramref name="skinInfo"/> can be mounted via <see cref="RealmArchiveModelImporter{TModel}.BeginExternalEditing"/>.
        /// </summary>
        public static bool CanUseRealmExternalEdit(Live<SkinInfo> skinInfo) => CanUseRealmExternalEdit(skinInfo.Value, skinInfo.IsManaged);

        /// <summary>
        /// Resolves the scripted skin folder under <c>EzResources/ScriptedSkin</c>.
        /// </summary>
        public static string? GetScriptDirectory(SkinInfo skinInfo, string scriptBasePath)
        {
            string scriptedSkinRoot = Path.Combine(scriptBasePath, "ScriptedSkin");

            if (!string.IsNullOrWhiteSpace(skinInfo.Hash))
            {
                string hashDirectory = Path.Combine(scriptedSkinRoot, skinInfo.Hash);

                if (Directory.Exists(hashDirectory))
                    return hashDirectory;
            }

            if (skinInfo.Name.StartsWith("[Script] ", StringComparison.Ordinal))
            {
                string skinName = skinInfo.Name.Substring("[Script] ".Length);
                string scriptDirectory = Path.Combine(scriptedSkinRoot, skinName);

                if (Directory.Exists(scriptDirectory))
                    return scriptDirectory;
            }

            string directDirectory = Path.Combine(scriptedSkinRoot, skinInfo.Name);
            return Directory.Exists(directDirectory) ? directDirectory : null;
        }

        /// <summary>
        /// Resolves the primary <c>.csx</c> file for a scripted skin.
        /// </summary>
        public static string? GetScriptPath(SkinInfo skinInfo, string scriptBasePath)
        {
            string? scriptDirectory = GetScriptDirectory(skinInfo, scriptBasePath);
            return scriptDirectory == null ? null : FindPrimaryScriptPath(scriptDirectory);
        }

        /// <summary>
        /// Serialises HUD layout JSON files into the scripted skin directory.
        /// </summary>
        /// <returns>Whether any file content changed.</returns>
        public static bool SaveLayoutToScriptDirectory(Skin skin, string scriptBasePath)
        {
            if (!IsScriptedSkin(skin.SkinInfo.Value))
                return false;

            string? directory = GetScriptDirectory(skin.SkinInfo.Value, scriptBasePath);

            if (string.IsNullOrEmpty(directory))
            {
                Logger.Log("Cannot save scripted skin layout: script directory not found.", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }

            bool hadChanges = false;

            foreach (var drawableInfo in skin.LayoutInfos)
            {
                string filename = $"{drawableInfo.Key}.json";
                string path = Path.Combine(directory, filename);
                string json = JsonConvert.SerializeObject(drawableInfo.Value, new JsonSerializerSettings { Formatting = Formatting.Indented });
                string? existing = File.Exists(path) ? File.ReadAllText(path) : null;

                if (existing == json)
                    continue;

                try
                {
                    File.WriteAllText(path, json, Encoding.UTF8);
                    hadChanges = true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to write scripted skin layout file '{filename}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            }

            return hadChanges;
        }

        internal static string? FindPrimaryScriptPath(string scriptDirectory)
        {
            string[] preferred = Directory.GetFiles(scriptDirectory, "*Skin.csx", SearchOption.TopDirectoryOnly);

            if (preferred.Length > 0)
                return preferred.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).First();

            string skinFile = Path.Combine(scriptDirectory, "Skin.csx");

            if (File.Exists(skinFile))
                return skinFile;

            return Directory.GetFiles(scriptDirectory, "*.csx", SearchOption.TopDirectoryOnly)
                            .Where(file => !Path.GetFileNameWithoutExtension(file).EndsWith("Transformer", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
        }
    }
}
