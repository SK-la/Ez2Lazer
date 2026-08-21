// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Pets
{
    public enum EzPetRendererKind
    {
        Frames,
        Live2D,
    }

    /// <summary>
    /// Official Live2D preset gate. Scan still lists any folder with pet.json, but Cubism
    /// (and <see cref="EzPetPack.Live2DAuthorized"/>) only applies when pack id + entry hash match.
    /// </summary>
    public static partial class EzPetLive2DAccess
    {
        /// <summary>
        /// Pack folder name (case-insensitive) → SHA-256 hex of the canonical model entry
        /// (prefer <c>live2d/*.model3.json</c>, else first <c>.moc3</c>).
        /// Empty until official presets are registered.
        /// </summary>
        public static IReadOnlyDictionary<string, string> PresetHashes { get; private set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void SetPresetHashesForTests(IReadOnlyDictionary<string, string> hashes)
        {
            PresetHashes = new Dictionary<string, string>(hashes, StringComparer.OrdinalIgnoreCase);
        }

        public static EzPetRendererKind ParseRenderer(string? renderer)
        {
            if (string.Equals(renderer, "live2d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderer, "l2d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderer, "cubism", StringComparison.OrdinalIgnoreCase))
                return EzPetRendererKind.Live2D;

            return EzPetRendererKind.Frames;
        }

        /// <summary>
        /// True only when the pack asks for Live2D and passes whitelist + hash.
        /// </summary>
        public static bool TryAuthorize(string packName, EzPetPackDefinition definition, Storage petsStorage, out string? denialReason)
        {
            denialReason = null;

            if (ParseRenderer(definition.Renderer) != EzPetRendererKind.Live2D)
            {
                denialReason = "renderer is not live2d";
                return false;
            }

            if (!PresetHashes.TryGetValue(packName, out string? expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
            {
                denialReason = $"pack '{packName}' is not an official Live2D preset";
                Logger.Log($"Ez pet: Live2D denied for '{packName}' (not in preset whitelist).", LoggingTarget.Runtime);
                return false;
            }

            string? entryRelative = FindCanonicalEntryRelativePath(petsStorage, packName, definition);
            if (entryRelative == null)
            {
                denialReason = "missing live2d model entry";
                Logger.Log($"Ez pet: Live2D denied for '{packName}' (no model3.json/moc3).", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }

            string? actual = ComputeFileSha256Hex(petsStorage, entryRelative);
            if (actual == null)
            {
                denialReason = "failed to hash live2d entry";
                return false;
            }

            if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                denialReason = "live2d payload hash mismatch";
                Logger.Log($"Ez pet: Live2D denied for '{packName}' (hash mismatch).", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }

            return true;
        }

        public static string? FindCanonicalEntryRelativePath(Storage petsStorage, string packName, EzPetPackDefinition definition)
        {
            string live2dRoot = string.IsNullOrWhiteSpace(definition.Live2D?.Root)
                ? "live2d"
                : definition.Live2D!.Root.Trim().Replace('\\', '/').Trim('/');

            string baseDir = Path.Combine(packName, live2dRoot.Replace('/', Path.DirectorySeparatorChar));

            if (!string.IsNullOrWhiteSpace(definition.Live2D?.Model))
            {
                string modelRel = Path.Combine(baseDir, definition.Live2D!.Model.Replace('/', Path.DirectorySeparatorChar));
                if (petsStorage.Exists(modelRel))
                    return modelRel.Replace('\\', '/');
            }

            try
            {
                foreach (string file in petsStorage.GetFiles(baseDir, "*.model3.json"))
                    return file.Replace('\\', '/');

                foreach (string file in petsStorage.GetFiles(baseDir, "*.moc3"))
                    return file.Replace('\\', '/');
            }
            catch
            {
                // missing directory
            }

            return null;
        }

        public static string? ComputeFileSha256Hex(Storage storage, string relativePath)
        {
            try
            {
                using var stream = storage.GetStream(relativePath);
                if (stream == null)
                    return null;

                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ez pet: failed hashing '{relativePath}'");
                return null;
            }
        }
    }

    public class EzPetLive2DDefinition
    {
        /// <summary>
        /// Subfolder under the pack root (default <c>live2d</c>).
        /// </summary>
        public string Root { get; set; } = "live2d";

        /// <summary>
        /// Optional relative path under <see cref="Root"/> to the <c>.model3.json</c> (or moc3) entry.
        /// </summary>
        public string Model { get; set; } = string.Empty;
    }
}
