// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Edit.Scenes;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSkinScopedGroupRegistry
    {
        public static IReadOnlyList<IEzSkinEditorSkinScopedGroupProvider> All { get; } = new IEzSkinEditorSkinScopedGroupProvider[]
        {
            new SkinScopedGroupProvider(EzSkinEditorSkinKind.EzStylePro),
            new SkinScopedGroupProvider(EzSkinEditorSkinKind.Ez2),
            new SkinScopedGroupProvider(EzSkinEditorSkinKind.SbI),
            new SkinScopedGroupProvider(EzSkinEditorSkinKind.Scripted),
        };

        public static IEnumerable<IEzSkinEditorSkinScopedGroupProvider> For(EzSkinEditorSkinKind kind) =>
            All.Where(p => p.Kind == kind);

        /// <summary>
        /// 皮肤专用分组。所有皮肤种类共用同一导航标题与稳定 Id，差异只在分组内容。
        /// </summary>
        private sealed class SkinScopedGroupProvider : IEzSkinEditorSkinScopedGroupProvider
        {
            public SkinScopedGroupProvider(EzSkinEditorSkinKind kind)
            {
                Kind = kind;
            }

            public EzSkinEditorSkinKind Kind { get; }

            public bool IsApplicable(EzSkinEditorSceneContext context) =>
                EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin) == Kind;

            public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateGroups(EzSkinEditorSceneContext context) =>
                new[]
                {
                    new EzSkinEditorSidebarGroupDefinition
                    {
                        Id = AppearanceSceneStrategy.GROUP_ID_SKIN_SCOPED,
                        Title = EzEditorStrings.GROUP_SKIN_SPECIFIC,
                        CreateContent = () => Kind == EzSkinEditorSkinKind.Scripted
                            ? new EzSkinEditorScriptedSkinSettingsSection()
                            : new EzBuiltInSkinScopedSettingsControls(Kind),
                    },
                };
        }
    }
}
