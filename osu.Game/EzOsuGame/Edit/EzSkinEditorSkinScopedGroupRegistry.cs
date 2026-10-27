// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorSkinScopedGroupRegistry
    {
        public static IReadOnlyList<IEzSkinEditorSkinScopedGroupProvider> All { get; } = new IEzSkinEditorSkinScopedGroupProvider[]
        {
            new EzStyleProSkinScopedGroupProvider(),
            new Ez2SkinScopedGroupProvider(),
            new SbISkinScopedGroupProvider(),
            new ScriptedSkinScopedGroupProvider(),
        };

        public static IEnumerable<IEzSkinEditorSkinScopedGroupProvider> For(EzSkinEditorSkinKind kind) =>
            All.Where(p => p.Kind == kind);

        private abstract class BuiltInSkinScopedGroupProviderBase : IEzSkinEditorSkinScopedGroupProvider
        {
            public abstract EzSkinEditorSkinKind Kind { get; }

            protected abstract LocalisableString GroupTitle { get; }

            public bool IsApplicable(EzSkinEditorSceneContext context) =>
                EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin) == Kind;

            public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateGroups(EzSkinEditorSceneContext context) =>
                new[]
                {
                    new EzSkinEditorSidebarGroupDefinition
                    {
                        Title = GroupTitle,
                        CreateContent = () => new EzBuiltInSkinScopedSettingsControls(),
                    },
                };
        }

        private sealed class EzStyleProSkinScopedGroupProvider : BuiltInSkinScopedGroupProviderBase
        {
            public override EzSkinEditorSkinKind Kind => EzSkinEditorSkinKind.EzStylePro;

            protected override LocalisableString GroupTitle => EzEditorStrings.GROUP_EZ_STYLE_PRO_SCOPED;
        }

        private sealed class Ez2SkinScopedGroupProvider : BuiltInSkinScopedGroupProviderBase
        {
            public override EzSkinEditorSkinKind Kind => EzSkinEditorSkinKind.Ez2;

            protected override LocalisableString GroupTitle => EzEditorStrings.GROUP_EZ2_SCOPED;
        }

        private sealed class SbISkinScopedGroupProvider : BuiltInSkinScopedGroupProviderBase
        {
            public override EzSkinEditorSkinKind Kind => EzSkinEditorSkinKind.SbI;

            protected override LocalisableString GroupTitle => EzEditorStrings.GROUP_SBI_SCOPED;
        }

        private sealed class ScriptedSkinScopedGroupProvider : IEzSkinEditorSkinScopedGroupProvider
        {
            public EzSkinEditorSkinKind Kind => EzSkinEditorSkinKind.Scripted;

            public bool IsApplicable(EzSkinEditorSceneContext context) =>
                EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin) == Kind;

            public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateGroups(EzSkinEditorSceneContext context) =>
                new[]
                {
                    new EzSkinEditorSidebarGroupDefinition
                    {
                        Title = EzEditorStrings.GROUP_SCRIPTED_SKIN_SCOPED,
                        CreateContent = () => new ScriptedSkinScopedPlaceholderSection(),
                    },
                };

            // Future: [SkinConfig] reflection UI + csx write-back footer can live here.
            public Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context) => null;
        }
    }
}
