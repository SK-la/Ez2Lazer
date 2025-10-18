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
using osu.Game.Storyboards.Drawables;
using osuTK;

namespace osu.Game.Screens.Backgrounds
{
    /// <summary>
    /// Mania模式的背景屏幕，具有模糊叠加层，遮罩到游戏面板宽度。
    /// </summary>
    public partial class BackgroundScreenBeatmapMania : BackgroundScreenBeatmap
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
        private WorkingBeatmap beatmap => Beatmap;

        [Resolved]
        private EzSkinSettingsManager ezSkinSettings { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public BackgroundScreenBeatmapMania(WorkingBeatmap beatmap)
            : base(beatmap)
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            maskingContainer = new Container
            {
                Masking = true,
                RelativeSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0.9f
            };

            blurContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            };

            replicatedBackground = new BeatmapBackground(Beatmap)
            {
                RelativeSizeAxes = Axes.Both
            };

            blurContainer.Add(replicatedBackground);
            replicatedBackground.BlurTo(new Vector2(BlurAmount.Value), 0);

            // 添加暗化Box，与标准模式一致
            dimBox = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Black,
                Alpha = 0
            };
            blurContainer.Add(dimBox);

            // 如果beatmap有Storyboard，添加Storyboard overlay
            if (Beatmap.Storyboard.HasDrawable)
            {
                var drawableStoryboard = new DrawableStoryboard(Beatmap.Storyboard)
                {
                    RelativeSizeAxes = Axes.Both
                };
                blurContainer.Add(drawableStoryboard);
            }

            maskingContainer.Add(blurContainer);
            AddInternal(maskingContainer);

            columnBlur = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnBlur);
            columnWidth = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinSettings.GetBindable<double>(EzSkinSetting.SpecialFactor);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 绑定暗化Box到用户设置
            var dimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
            dimLevel.BindValueChanged(v => dimBox.Alpha = (float)v.NewValue, true);

            // 让复刻背景完全复制原背景的显示效果
            replicatedBackground.RelativeSizeAxes = Axes.None;
            replicatedBackground.Size = new Vector2(DrawWidth, DrawHeight);

            columnBlur.BindValueChanged(e =>
            {
                replicatedBackground?.BlurTo(new Vector2((float)e.NewValue * 30f), 0);
            }, true);

            // BlurAmount.BindValueChanged(v => replicatedBackground?.BlurTo(new Vector2(v.NewValue), 0), true);
            columnWidth.BindValueChanged(e => updateWidth(), true);
            specialFactor.BindValueChanged(e => updateWidth(), true);
            updateWidth();
        }

        private void updateWidth()
        {
            if (beatmap?.Beatmap?.BeatmapInfo.Difficulty.CircleSize != null)
                totalColumns = beatmap.BeatmapInfo.Difficulty.CircleSize;

            int keyMode = (int)totalColumns;
            totalWidth = 0;

            for (int i = 0; i < keyMode; i++) totalWidth += getColumnWidth(keyMode, i);

            maskingContainer.Width = totalWidth * 0.94f;
        }

        private float getColumnWidth(int keyMode, int columnIndex)
        {
            bool isSpecialColumn = ezSkinSettings.GetColumnType(keyMode, columnIndex) == "S";
            float baseWidth = (float)columnWidth.Value;
            float factor = (float)specialFactor.Value;
            return baseWidth * (isSpecialColumn ? factor : 1.0f);
        }
        // TODO: 如果需要，实现动态模糊（例如，绑定到音频响应）
    }
}
