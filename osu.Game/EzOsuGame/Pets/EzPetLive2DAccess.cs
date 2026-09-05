// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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
    /// Live2D pack gate: Cubism runs when <c>renderer</c> is live2d and a model entry exists.
    /// Cubism Core DLL is checked at session create time, not here.
    /// </summary>
    public static class EzPetLive2DAccess
    {
        public static EzPetRendererKind ParseRenderer(string? renderer)
        {
            if (string.Equals(renderer, "live2d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderer, "l2d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderer, "cubism", StringComparison.OrdinalIgnoreCase))
                return EzPetRendererKind.Live2D;

            return EzPetRendererKind.Frames;
        }

        /// <summary>
        /// True when the pack asks for Live2D and a <c>.model3.json</c> / <c>.moc3</c> entry is present.
        /// </summary>
        public static bool TryAuthorize(string packName, EzPetPackDefinition definition, Storage petsStorage, out string? denialReason)
        {
            denialReason = null;

            if (ParseRenderer(definition.Renderer) != EzPetRendererKind.Live2D)
            {
                denialReason = "renderer is not live2d";
                return false;
            }

            string? entryRelative = FindCanonicalEntryRelativePath(petsStorage, packName, definition);

            if (entryRelative == null)
            {
                denialReason = "missing live2d model entry";
                Logger.Log($"Ez pet: Live2D denied for '{packName}' (no model3.json/moc3).", LoggingTarget.Runtime, LogLevel.Error);
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

        /// <summary>
        /// Whether <c>Live2DCubismCore.dll</c> is present under Pets/_cubism (does not load it).
        /// </summary>
        public static bool HasCubismCoreOnDisk(Storage petsStorage)
            => petsStorage.Exists(Path.Combine(EzPetCubismNative.CORE_DIRECTORY, EzPetCubismNative.CORE_DLL_WINDOWS));
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
