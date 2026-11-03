// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSkinKindResolver
    {
        public static EzSkinEditorSkinKind Resolve(ISkin? skin)
        {
            if (skin is ScriptedSkinWrapper)
                return EzSkinEditorSkinKind.Scripted;

            if (skin is EzStyleProSkin)
                return EzSkinEditorSkinKind.EzStylePro;

            if (skin is Ez2Skin)
                return EzSkinEditorSkinKind.Ez2;

            if (skin is SbISkin)
                return EzSkinEditorSkinKind.SbI;

            return EzSkinEditorSkinKind.None;
        }

        public static EzSkinEditorSkinKind Resolve(SkinInfo? skinInfo)
        {
            if (skinInfo == null)
                return EzSkinEditorSkinKind.None;

            if (ScriptedSkinSupport.IsScriptedSkin(skinInfo))
                return EzSkinEditorSkinKind.Scripted;

            string info = skinInfo.InstantiationInfo;

            if (info.Contains(nameof(EzStyleProSkin), System.StringComparison.Ordinal))
                return EzSkinEditorSkinKind.EzStylePro;

            if (info.Contains(nameof(Ez2Skin), System.StringComparison.Ordinal))
                return EzSkinEditorSkinKind.Ez2;

            if (info.Contains(nameof(SbISkin), System.StringComparison.Ordinal))
                return EzSkinEditorSkinKind.SbI;

            return EzSkinEditorSkinKind.None;
        }
    }
}
