// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.HUD
{
    public static class EzGameThemeNames
    {
        public const string DEFAULT = "Celeste_Lumiere";

        /// <summary>
        /// 皮肤里 ThemeName 曾按 enum 存成数字字符串；解析为文件夹名后再用于列表与纹理路径。
        /// </summary>
        public static string Resolve(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();

            if (int.TryParse(trimmed, out int ordinal) && Enum.IsDefined(typeof(EzEnumGameThemeName), ordinal))
                return ((EzEnumGameThemeName)ordinal).ToString();

            if (Enum.TryParse(trimmed, out EzEnumGameThemeName named))
                return named.ToString();

            return trimmed;
        }

        public static string Normalize(string? value)
        {
            string resolved = Resolve(value);
            return string.IsNullOrWhiteSpace(resolved) ? DEFAULT : resolved;
        }
    }
}
