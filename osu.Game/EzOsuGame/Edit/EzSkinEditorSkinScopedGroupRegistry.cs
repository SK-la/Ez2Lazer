// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Edit.Settings.Sections;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSkinScopedGroupRegistry
    {
        public static IReadOnlyList<IEzSkinEditorSkinScopedGroupProvider> All { get; } = new IEzSkinEditorSkinScopedGroupProvider[]
        {
            new SkinScopedGroupProvider(EzSkinFamily.EzStylePro),
            new SkinScopedGroupProvider(EzSkinFamily.Ez2),
            new SkinScopedGroupProvider(EzSkinFamily.SbI),
            new SkinScopedGroupProvider(EzSkinFamily.Scripted),
        };

        public static IEnumerable<IEzSkinEditorSkinScopedGroupProvider> For(EzSkinFamily kind) =>
            All.Where(p => p.Kind == kind);

        /// <summary>
        /// 皮肤专用栏目的内容。所有皮肤种类共用同一导航标题，差异只在控件。
        /// </summary>
        private sealed class SkinScopedGroupProvider : IEzSkinEditorSkinScopedGroupProvider
        {
            public SkinScopedGroupProvider(EzSkinFamily kind)
            {
                Kind = kind;
            }

            public EzSkinFamily Kind { get; }

            public Drawable CreateControls() => Kind == EzSkinFamily.Scripted
                ? new EzSkinEditorScriptedSkinSettingsSection()
                : new EzBuiltInSkinScopedSettingsControls(Kind);
        }
    }
}
