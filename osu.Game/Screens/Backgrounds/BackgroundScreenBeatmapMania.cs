// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osuTK;
using System.Reflection;

namespace osu.Game.Screens.Backgrounds
{
    /// <summary>
    /// Mania模式的背景组件，具有模糊叠加层，遮罩到游戏面板宽度。
    /// </summary>
    public partial class BackgroundScreenBeatmapMania : CompositeDrawable
    {
        private Container blurContainer;
        private Container maskingContainer;
        private Background replicatedBackground;
        private Box dimBox;
        private float totalColumns;
        private float totalWidth;

        private Bindable<double> columnWidth;
        private Bindable<double> specialFactor;
        private IBindable<double> columnBlur;
        private Bindable<ScalingMode> scalingMode;
        private Bindable<bool> showStoryboard;
        private Bindable<float> uiScale;
        private readonly WorkingBeatmap beatmap;

        [Resolved]
        private EzSkinSettingsManager ezSkinSettings { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public BackgroundScreenBeatmapMania(WorkingBeatmap beatmap)
        {
            this.beatmap = beatmap;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnBlur = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnBlur);
            columnWidth = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinSettings.GetBindable<double>(EzSkinSetting.SpecialFactor);
            scalingMode = config.GetBindable<ScalingMode>(OsuSetting.Scaling);
            showStoryboard = config.GetBindable<bool>(OsuSetting.ShowStoryboard);
            uiScale = config.GetBindable<float>(OsuSetting.UIScale);

            maskingContainer = new Container
            {
                Masking = true,
                RelativeSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0.98f,
                Child = blurContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        // Dim box to darken the background behind the playfield.
                        dimBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.Black,
                            Alpha = 0
                        },
                        replicatedBackground = showStoryboard.Value
                            ? new BeatmapBackgroundWithStoryboard(beatmap)
                            : new BeatmapBackground(beatmap)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Scale = new Vector2(1.8f),
                            },
                    }
                }
            };

            AddInternal(maskingContainer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 绑定暗化Box到用户设置
            var dimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
            dimLevel.BindValueChanged(v => dimBox.Alpha = (float)v.NewValue, true);

            columnBlur.BindValueChanged(e =>
            {
                replicatedBackground?.BlurTo(new Vector2((float)e.NewValue * 80f), 0);

                // Also apply blur to storyboard if present
                if (replicatedBackground is BeatmapBackgroundWithStoryboard bws)
                {
                    var storyboardContainerField = typeof(BeatmapBackgroundWithStoryboard).GetField("storyboardContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                    // if (storyboardContainerField?.GetValue(bws) is Container storyboardContainer)
                    //     storyboardContainer.BlurSigma = new Vector2((float)e.NewValue * 80f);
                }
            }, true);

            columnWidth.BindValueChanged(e => updateWidth(), true);
            specialFactor.BindValueChanged(e => updateWidth(), true);
            scalingMode.BindValueChanged(e => updateWidth(), true);
            uiScale.BindValueChanged(e => updateWidth(), true);
            updateWidth();
        }

        private void updateWidth()
        {
            if (beatmap?.Beatmap?.BeatmapInfo.Difficulty.CircleSize != null)
                totalColumns = beatmap.BeatmapInfo.Difficulty.CircleSize;

            int keyMode = (int)totalColumns;
            totalWidth = 0;

            for (int i = 0; i < keyMode; i++)
                totalWidth += getColumnWidth(keyMode, i);

            float totalScale = uiScale.Value / 0.936f;
            maskingContainer.Width = totalWidth / totalScale;
        }

        private float getColumnWidth(int keyMode, int columnIndex)
        {
            bool isSpecialColumn = ezSkinSettings.GetColumnType(keyMode, columnIndex) == "S";
            float baseWidth = (float)columnWidth.Value;
            float factor = (float)specialFactor.Value;
            return baseWidth * (isSpecialColumn ? factor : 1.0f);
        }
    }
}
