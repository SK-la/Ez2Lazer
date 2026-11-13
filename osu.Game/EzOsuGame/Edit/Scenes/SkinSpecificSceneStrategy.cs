// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 皮肤专用场景占位壳；实际专用设置在 Appearance 场景侧栏的「专用」分组。
    /// 按钮永久隐藏，故 <see cref="IsAvailable"/> 为 false。
    /// </summary>
    public class SkinSpecificSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzEditorSceneType SceneType => EzEditorSceneType.SkinSpecific;

        public EzEditorSceneGroup Group => EzEditorSceneGroup.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.TAB_SKIN_SPECIFIC;

        public bool IsAvailable => false;

        public void ReleaseCachedDrawables() => sections = null;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new SkinSpecificScenePlaceholderSection(),
        };
    }

    public partial class SkinSpecificScenePlaceholderSection : EzSkinEditorSettingsGroup
    {
        public SkinSpecificScenePlaceholderSection()
            : base(EzEditorStrings.TAB_SKIN_SPECIFIC)
        {
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
