// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 皮肤专用场景占位壳；实际专用设置在 Appearance 场景侧栏的「专用」分组。
    /// </summary>
    public class SkinSpecificSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SkinSpecific;

        public LocalisableString TabTitle => EzEditorStrings.TAB_SKIN_SPECIFIC;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.TAB_SKIN_SPECIFIC,
                    CreateContent = () => new SkinSpecificScenePlaceholderSection(),
                },
            };
    }

    public partial class SkinSpecificScenePlaceholderSection : FillFlowContainer
    {
        public SkinSpecificScenePlaceholderSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Padding = new MarginPadding(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = EzEditorStrings.SKIN_SPECIFIC_SCENE_PLACEHOLDER,
                Font = OsuFont.Default.With(size: 14),
            };
        }
    }
}
