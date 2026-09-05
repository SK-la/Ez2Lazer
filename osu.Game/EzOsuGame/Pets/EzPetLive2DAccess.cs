// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
            string live2DRoot = string.IsNullOrWhiteSpace(definition.Live2D?.Root)
                ? "live2d"
                : definition.Live2D!.Root.Trim().Replace('\\', '/').Trim('/');

            string baseDir = Path.Combine(packName, live2DRoot.Replace('/', Path.DirectorySeparatorChar));

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
        /// Whether the current-platform Cubism Core dynamic library is present under Pets/_cubism (does not load it).
        /// </summary>
        public static bool HasCubismCoreOnDisk(Storage petsStorage)
            => EzPetCubismNative.HasCubismCoreOnDisk(petsStorage);
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

        /// <summary>
        /// Clip/state id → simultaneous expression ids (e.g. rankSS → smile+wave+jump).
        /// </summary>
        public Dictionary<string, List<string>> ClipExpressions { get; set; } =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional override of built-in expression recipes by id.
        /// </summary>
        public Dictionary<string, EzPetExpressionRecipe> Expressions { get; set; } =
            new Dictionary<string, EzPetExpressionRecipe>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional clip → motion3 key when file name differs from clip id.
        /// </summary>
        public Dictionary<string, string> ClipMotions { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public EzPetLive2DLipSyncDefinition? LipSync { get; set; }
    }

    public class EzPetLive2DLipSyncDefinition
    {
        /// <summary>
        /// When true, BPM quarter-note mouth open while the track is playing. Default off; independent of the settings music-association toggle.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Minimum <c>ParamMouthOpenY</c> while mouth sync is active (never fully closed).
        /// </summary>
        public float MinOpen { get; set; } = 0.25f;
    }
}
