// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌组的谱面预览场景占位壳。仅用于全谱预览，后续用来修改每种 note 的颜色与厚度。
    /// 仅 debug 构建可见，功能未实现。
    /// </summary>
    public class BeatmapPreviewSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.BeatmapPreview;

        public EzSkinEditorSceneGroup Group => EzSkinEditorSceneGroup.SongSelect;

        public LocalisableString TabTitle => EzEditorStrings.TAB_BEATMAP_PREVIEW;

        public bool IsAvailable => DebugUtils.IsDebugBuild;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.TAB_BEATMAP_PREVIEW,
                    CreateContent = () => new BeatmapPreviewScenePlaceholderSection(),
                },
            };
    }

    public partial class BeatmapPreviewScenePlaceholderSection : FillFlowContainer
    {
        public BeatmapPreviewScenePlaceholderSection()
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
                Text = EzEditorStrings.BEATMAP_PREVIEW_SCENE_PLACEHOLDER,
                Font = OsuFont.Default.With(size: 14),
            };
        }
    }
}
