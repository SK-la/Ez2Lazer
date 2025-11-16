// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Backgrounds;
using osuTK;

namespace osu.Game.Screens.Play
{
    public partial class Player
    {
        public partial class PlayerManiaBackgroundScreen : BackgroundScreenBeatmap
        {
            private readonly Player player;
            private Container maniaBackgroundMask = null!;
            private DimmableBackground maniaMaskedDimmable = null!;
            private Vector2 lastDrawSize;

            // Mania 专用配置（供自定义背景使用）
            private Bindable<double> maniaColumnBlur = new Bindable<double>();
            private Bindable<double> maniaColumnWidth = new Bindable<double>();
            private Bindable<double> maniaSpecialFactor = new Bindable<double>();
            private Bindable<float> uiScale = new Bindable<float>(1f);

            private int keyMode;

            [Resolved]
            private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

            public PlayerManiaBackgroundScreen(WorkingBeatmap beatmap, Player player)
                : base(beatmap)
            {
                this.player = player;
                DisableParallax = true;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                keyMode = (int)player.Beatmap.Value.Beatmap.BeatmapInfo.Difficulty.CircleSize;

                // 创建遮罩背景容器
                // 关键：不使用嵌套结构，直接让 DimmableBackground 作为遮罩容器的子元素
                maniaBackgroundMask = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = maniaMaskedDimmable = new DimmableBackground
                    {
                        RelativeSizeAxes = Axes.None, // 不使用相对尺寸，如果用Both会导致主副背景缩放不一致
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }
                };

                AddInternal(maniaBackgroundMask);

                // 设置副本背景
                var maskedBackground = new BeatmapBackground(player.Beatmap.Value);
                maskedBackground.FadeInFromZero(500, Easing.OutQuint);
                maniaMaskedDimmable.Background = maskedBackground;
                maniaMaskedDimmable.StoryboardReplacesBackground.BindTo(player.storyboardReplacesBackground);
                maniaMaskedDimmable.IgnoreUserSettings.BindTo(new Bindable<bool>(true));
                maniaMaskedDimmable.IsBreakTime.BindTo(player.IsBreakTime);

                maniaColumnBlur = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnBlur);
                maniaColumnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
                maniaSpecialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);

                maniaColumnBlur.BindValueChanged(v => maniaMaskedDimmable.BlurAmount.Value = (float)v.NewValue * 50, true);
                maniaColumnWidth.BindValueChanged(_ => updateMaskWidth(), true);
                maniaSpecialFactor.BindValueChanged(_ => updateMaskWidth(), true);

                uiScale = config.GetBindable<float>(OsuSetting.UIScale);
                uiScale.BindValueChanged(_ => updateMaskWidth(), true);

                updateMaskWidth();
            }

            protected override void Update()
            {
                base.Update();

                if (lastDrawSize != DrawSize)
                {
                    lastDrawSize = DrawSize;
                    maniaMaskedDimmable.Size = DrawSize;
                }
            }

            private void updateMaskWidth()
            {
                if (!player.LoadedBeatmapSuccessfully) return;

                float totalWidth = ezSkinConfig.GetTotalWidth(keyMode);

                float uiScaleCompensation = 1f / uiScale.Value;

                maniaBackgroundMask.Width = totalWidth * uiScaleCompensation;
            }
        }
    }
}

// 尝试通过反射获取ManiaPlayfield的实际下落面板宽度, 反射有延迟且不稳定，先注释掉，不要删除
// if (player.DrawableRuleset?.Playfield is ScrollingPlayfield scrollingPlayfield)
// {
//     try
//     {
//         // 通过反射访问ManiaPlayfield的私有stages字段
//         var stagesField = scrollingPlayfield.GetType().GetField("stages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//
//         if (stagesField != null)
//         {
//             if (stagesField.GetValue(scrollingPlayfield) is IList stages && stages.Count > 0)
//             {
//                 // 获取第一个Stage的DrawWidth（Mania通常只有一个Stage）
//                 object? firstStage = stages[0];
//                 var drawWidthProperty = firstStage?.GetType().GetProperty("DrawWidth");
//
//                 if (drawWidthProperty != null)
//                 {
//                     totalWidth = (float)drawWidthProperty.GetValue(firstStage)!;
//                 }
//             }
//         }
//     }
//     catch
//     {
//         // 如果反射失败，回退到计算列宽总和
//         totalWidth = 0;
//     }
// }
