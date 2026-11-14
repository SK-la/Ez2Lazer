// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Configuration
{
    /// <summary>
    /// 列类型（A/B/S/E/P）查 Ez Pro note、ColumnLight 等共享资源时的降级顺序、路径前缀与部件名。
    /// 查资源时从当前列类型起沿 <see cref="DEGRADE_ORDER"/> 向后尝试；具体字符串仅定义于此。
    /// </summary>
    public static class EzColumnResourceLookup
    {
        /// <summary>
        /// 固定降级序：E → P → S → B → A（A 仅自身，B→A，S→BA，E→PSBA，P→SBA）。
        /// </summary>
        public static readonly IReadOnlyList<EzColumnType> DEGRADE_ORDER = new[] { EzColumnType.E, EzColumnType.P, EzColumnType.S, EzColumnType.B, EzColumnType.A };

        public const string NOTE_COMPONENT = "note";
        public const string LN_HEAD_COMPONENT = "longnote/head";
        public const string LN_TAIL_COMPONENT = "longnote/tail";
        public const string LN_BODY_COMPONENT = "longnote/middle";

        public const string NOTE_TEXTURE_AFFIX_A = "white";
        public const string NOTE_TEXTURE_AFFIX_B = "blue";
        public const string NOTE_TEXTURE_AFFIX_S = "red";
        public const string NOTE_TEXTURE_AFFIX_E = "red";
        public const string NOTE_TEXTURE_AFFIX_P = "green";

        /// <summary>
        /// 列色着色开启时 Ez Pro note 使用的统一白底前缀。
        /// </summary>
        public const string TINT_BASE_TEXTURE_AFFIX = NOTE_TEXTURE_AFFIX_A;

        /// <summary>
        /// 从 <paramref name="columnType"/> 起按 <see cref="DEGRADE_ORDER"/> 依次枚举可尝试的列类型。
        /// </summary>
        public static IEnumerable<EzColumnType> EnumerateDegradeOrder(EzColumnType columnType)
        {
            int start = IndexInDegradeOrder(columnType);

            if (start < 0)
                start = DEGRADE_ORDER.Count - 1;

            for (int i = start; i < DEGRADE_ORDER.Count; i++)
                yield return DEGRADE_ORDER[i];
        }

        public static int IndexInDegradeOrder(EzColumnType columnType)
        {
            for (int i = 0; i < DEGRADE_ORDER.Count; i++)
            {
                if (DEGRADE_ORDER[i] == columnType)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Ez Pro note 资源路径前缀（<c>note/{set}/</c> 下第一级目录名）。列色开时用白底贴图，由着色乘色。
        /// </summary>
        public static string EzProNoteTexturePrefix(EzColumnType columnType) => columnType switch
        {
            EzColumnType.A => NOTE_TEXTURE_AFFIX_A,
            EzColumnType.B => NOTE_TEXTURE_AFFIX_B,
            EzColumnType.S => NOTE_TEXTURE_AFFIX_S,
            EzColumnType.E => NOTE_TEXTURE_AFFIX_E,
            EzColumnType.P => NOTE_TEXTURE_AFFIX_P,
            _ => NOTE_TEXTURE_AFFIX_A,
        };

        /// <summary>
        /// ColumnLight 套装内按类型分色的子路径名（<c>ColumnLight/{set}/</c> 下）。与 <see cref="EzProNoteTexturePrefix"/> 同色，仅首字母大写。
        /// </summary>
        public static string ColumnLightColourName(EzColumnType columnType)
        {
            string prefix = EzProNoteTexturePrefix(columnType);
            return char.ToUpperInvariant(prefix[0]) + prefix.Substring(1);
        }

        public static string EzProNoteComponent(EzManiaNotePart part) => part switch
        {
            EzManiaNotePart.HoldHead => LN_HEAD_COMPONENT,
            EzManiaNotePart.HoldBody => LN_BODY_COMPONENT,
            EzManiaNotePart.HoldTail => LN_TAIL_COMPONENT,
            _ => NOTE_COMPONENT,
        };

        public static string FormatEzProNoteResource(string prefix, EzManiaNotePart part)
            => prefix + EzProNoteComponent(part);

        public static string FormatEzProNoteResource(string prefix, string componentAfterPrefix)
            => prefix + componentAfterPrefix;

        /// <summary>
        /// 皮肤编辑器导出等场景：按列类型主前缀拼接部件，不做降级链尝试。
        /// </summary>
        public static string ResolveEzProNoteResourceName(EzColumnType columnType, EzManiaNotePart part)
            => FormatEzProNoteResource(EzProNoteTexturePrefix(columnType), part);
    }
}
