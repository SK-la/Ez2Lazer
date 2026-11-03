// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSkinKindResolver
    {
        public static EzSkinFamily Resolve(ISkin? skin)
        {
            if (skin is ScriptedSkinWrapper)
                return EzSkinFamily.Scripted;

            if (skin is EzStyleProSkin)
                return EzSkinFamily.EzStylePro;

            if (skin is Ez2Skin)
                return EzSkinFamily.Ez2;

            if (skin is SbISkin)
                return EzSkinFamily.SbI;

            return EzSkinFamily.None;
        }

        public static EzSkinFamily Resolve(SkinInfo? skinInfo)
        {
            if (skinInfo == null)
                return EzSkinFamily.None;

            if (ScriptedSkinSupport.IsScriptedSkin(skinInfo))
                return EzSkinFamily.Scripted;

            string info = skinInfo.InstantiationInfo;

            if (info.Contains(nameof(EzStyleProSkin), System.StringComparison.Ordinal))
                return EzSkinFamily.EzStylePro;

            if (info.Contains(nameof(Ez2Skin), System.StringComparison.Ordinal))
                return EzSkinFamily.Ez2;

            if (info.Contains(nameof(SbISkin), System.StringComparison.Ordinal))
                return EzSkinFamily.SbI;

            return EzSkinFamily.None;
        }
    }
}
