// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Database;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// <c>skin.ini</c> applies only to traditionally imported legacy skins — not built-in Ez/official skins.
    /// </summary>
    public static class EzSkinIniSupport
    {
        public static bool IsSupported(SkinInfo skin) =>
            !skin.Protected && isLegacyInstantiation(skin.InstantiationInfo);

        public static bool IsSupported(Live<SkinInfo> skinInfo) =>
            skinInfo.PerformRead(IsSupported);

        private static bool isLegacyInstantiation(string instantiationInfo)
        {
            if (string.IsNullOrEmpty(instantiationInfo))
            {
                // Pre-metadata imports default to <see cref="LegacySkin"/> in <see cref="SkinInfo.CreateInstance"/>.
                return true;
            }

            // Format: "Namespace.TypeName, AssemblyName"
            string typeName = instantiationInfo.Split(',')[0];
            return typeName.EndsWith(nameof(LegacySkin), StringComparison.Ordinal);
        }
    }
}
