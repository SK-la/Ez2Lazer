// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Per-skin <c>EzSkin.json</c> applies to user-managed writable skins.
    /// </summary>
    public static class EzSkinJsonSupport
    {
        public static bool IsSupported(SkinInfo skin) => !skin.Protected && skin.IsManaged;

        public static bool IsSupported(Live<SkinInfo> skinInfo) => skinInfo.PerformRead(IsSupported);
    }
}
