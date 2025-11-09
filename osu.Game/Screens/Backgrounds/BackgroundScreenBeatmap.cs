// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Screens.Backgrounds
{
    public partial class BackgroundScreenBeatmap : BackgroundScreen
    {
        /// <summary>
        /// The amount of blur to apply when full user blur is requested.
        /// </summary>
        public const float USER_BLUR_FACTOR = 25;

        protected Background Background;

        private WorkingBeatmap beatmap;

        /// <summary>
        /// Whether or not user-configured settings relating to brightness of elements should be ignored.
        /// </summary>
        /// <remarks>
        /// Beatmap background screens should not apply user settings by default.
        /// </remarks>
        public readonly Bindable<bool> IgnoreUserSettings = new Bindable<bool>(true);

        /// <summary>
        /// Whether or not the storyboard loaded should completely hide the background behind it.
        /// </summary>
        public readonly Bindable<bool> StoryboardReplacesBackground = new Bindable<bool>();

        /// <summary>
        /// The amount of blur to be applied in addition to user-specified blur.
        /// </summary>
        public readonly Bindable<float> BlurAmount = new BindableFloat();

        /// <summary>
        /// The amount of dim to be used when <see cref="IgnoreUserSettings"/> is <c>true</c>.
        /// </summary>
        public readonly Bindable<float> DimWhenUserSettingsIgnored = new Bindable<float>();

        internal readonly Bindable<bool> IsBreakTime = new Bindable<bool>();

        private readonly DimmableBackground dimmable;

        protected virtual DimmableBackground CreateFadeContainer() => new DimmableBackground { RelativeSizeAxes = Axes.Both };

        // 双重绘制：底层完整背景，上层遮罩背景
        // private readonly Container maskedContainer;
        // private readonly DimmableBackground maskedDimmable; // 上层独立的 dimmable
        //
        // private Bindable<double> columnBlur;
        // private Bindable<double> columnWidth;
        // private Bindable<double> specialFactor;
        // private Bindable<float> uiScale;
        //
        // private int keyMode;
        //
        // [Resolved]
        // private EzSkinSettingsManager ezSkinSettings { get; set; } = null!;
        //
        // [Resolved]
        // private OsuConfigManager config { get; set; } = null!;

        public BackgroundScreenBeatmap(WorkingBeatmap beatmap = null)
        {
            Beatmap = beatmap;

            InternalChild = dimmable = CreateFadeContainer();

            dimmable.StoryboardReplacesBackground.BindTo(StoryboardReplacesBackground);
            dimmable.IgnoreUserSettings.BindTo(IgnoreUserSettings);
            dimmable.IsBreakTime.BindTo(IsBreakTime);
            dimmable.BlurAmount.BindTo(BlurAmount);
            dimmable.DimWhenUserSettingsIgnored.BindTo(DimWhenUserSettingsIgnored);

            // Container backgroundHolder;

            // if (Beatmap.BeatmapInfo.Ruleset.OnlineID == 3)
            // {
            //     // 上层：遮罩背景副本
            //     maskedContainer = new Container
            //     {
            //         RelativeSizeAxes = Axes.Y,
            //         Anchor = Anchor.Centre,
            //         Origin = Anchor.Centre,
            //         Masking = true,
            //         Alpha = 0,
            //         Children = new Drawable[]
            //         {
            //             // 暗化效果在Stage中实现，这里只负责创建背景副本虚化
            //             // 背景容器：保持全屏尺寸，不受父容器宽度限制
            //             backgroundHolder = new Container
            //             {
            //                 Anchor = Anchor.Centre,
            //                 Origin = Anchor.Centre,
            //                 Child = maskedDimmable = CreateFadeContainer()
            //             }
            //         }
            //     };
            //     AddInternal(maskedContainer);
            //
            //     maskedDimmable.StoryboardReplacesBackground.BindTo(StoryboardReplacesBackground);
            //     maskedDimmable.IgnoreUserSettings.BindTo(IgnoreUserSettings);
            //     maskedDimmable.IsBreakTime.BindTo(IsBreakTime);
            //
            //     // 设置 backgroundHolder 的尺寸为屏幕尺寸，不受父容器限制
            //     Schedule(() =>
            //     {
            //         backgroundHolder.Size = DrawSize;
            //     });
            // }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var background = new BeatmapBackground(beatmap);
            LoadComponent(background);
            switchBackground(background);

        //     // 如果是 Mania，绑定 EzSkin 设置到上层背景
        //     if (beatmap.BeatmapInfo.Ruleset.OnlineID == 3)
        //     {
        //         maskedContainer.Alpha = 1;
        //     }
        //
        //     keyMode = (int)Beatmap.BeatmapInfo.Difficulty.CircleSize;
        //
        //     columnBlur = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnBlur);
        //     columnWidth = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnWidth);
        //     specialFactor = ezSkinSettings.GetBindable<double>(EzSkinSetting.SpecialFactor);
        //     uiScale = config.GetBindable<float>(OsuSetting.UIScale);
        //
        //     columnWidth.BindValueChanged(_ => updateWidth(), true);
        //     specialFactor.BindValueChanged(_ => updateWidth(), true);
        //     uiScale.BindValueChanged(_ => updateWidth(), true);
        //     columnBlur.BindValueChanged(v => maskedDimmable.BlurAmount.Value = (float)v.NewValue * USER_BLUR_FACTOR, true);
        // }
        //
        // private void updateWidth()
        // {
        //     float totalWidth = 0;
        //
        //     for (int i = 0; i < keyMode; i++)
        //         totalWidth += getColumnWidth(keyMode, i);
        //
        //     maskedContainer.Width = totalWidth / uiScale.Value;
        // }
        //
        // private float getColumnWidth(int keyMode, int columnIndex)
        // {
        //     bool isSpecialColumn = ezSkinSettings.GetColumnType(keyMode, columnIndex) == "S";
        //     float baseWidth = (float)columnWidth.Value;
        //     float factor = (float)specialFactor.Value;
        //     return baseWidth * (isSpecialColumn ? factor : 1.0f);
        }

        private CancellationTokenSource cancellationSource;

        public WorkingBeatmap Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap == value && beatmap != null)
                    return;

                beatmap = value;

                Schedule(() =>
                {
                    if ((Background as BeatmapBackground)?.Beatmap.BeatmapInfo.BackgroundEquals(beatmap?.BeatmapInfo) ?? false)
                        return;

                    cancellationSource?.Cancel();
                    LoadComponentAsync(new BeatmapBackground(beatmap), switchBackground, (cancellationSource = new CancellationTokenSource()).Token);
                });
            }
        }

        private void switchBackground(BeatmapBackground b)
        {
            float newDepth = 0;

            if (Background != null)
            {
                newDepth = Background.Depth + 1;
                Background.FadeOut(250);
                Background.Expire();
            }

            b.Depth = newDepth;
            b.FadeInFromZero(500, Easing.OutQuint);
            dimmable.Background = Background = b;

            // // 上层：独立的背景（克隆一个新实例）
            // var maskedBackground = new BeatmapBackground(beatmap) { Depth = newDepth };
            // maskedBackground.Sprite.Texture = b.Sprite.Texture; // 直接使用底层背景的纹理，确保缩放一致
            // maskedDimmable.Background = maskedBackground;
        }

        public override bool Equals(BackgroundScreen other)
        {
            if (!(other is BackgroundScreenBeatmap otherBeatmapBackground)) return false;

            return base.Equals(other) && beatmap == otherBeatmapBackground.Beatmap;
        }

        public partial class DimmableBackground : UserDimContainer
        {
            /// <summary>
            /// The amount of blur to be applied to the background in addition to user-specified blur.
            /// </summary>
            /// <remarks>
            /// Used in contexts where there can potentially be both user and screen-specified blurring occuring at the same time, such as in <see cref="PlayerLoader"/>
            /// </remarks>
            public readonly Bindable<float> BlurAmount = new BindableFloat();

            public readonly Bindable<bool> StoryboardReplacesBackground = new Bindable<bool>();

            public Background Background
            {
                get => background;
                set
                {
                    background?.Expire();

                    base.Add(background = value);
                    background.BlurTo(blurTarget, 0, Easing.OutQuint);
                }
            }

            private Bindable<double> userBlurLevel { get; set; }

            private Background background;

            public override void Add(Drawable drawable)
            {
                ArgumentNullException.ThrowIfNull(drawable);

                if (drawable is Background)
                    throw new InvalidOperationException($"Use {nameof(Background)} to set a background.");

                base.Add(drawable);
            }

            /// <summary>
            /// As an optimisation, we add the two blur portions to be applied rather than actually applying two separate blurs.
            /// </summary>
            private Vector2 blurTarget => !IgnoreUserSettings.Value
                ? new Vector2(BlurAmount.Value + (float)userBlurLevel.Value * USER_BLUR_FACTOR)
                : new Vector2(BlurAmount.Value);

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                userBlurLevel = config.GetBindable<double>(OsuSetting.BlurLevel);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                userBlurLevel.ValueChanged += _ => UpdateVisuals();
                BlurAmount.ValueChanged += _ => UpdateVisuals();
                StoryboardReplacesBackground.ValueChanged += _ => UpdateVisuals();
            }

            protected override float DimLevel
            {
                get
                {
                    if ((IgnoreUserSettings.Value || ShowStoryboard.Value) && StoryboardReplacesBackground.Value)
                        return 1;

                    return base.DimLevel;
                }
            }

            protected override void UpdateVisuals()
            {
                base.UpdateVisuals();

                Background?.BlurTo(blurTarget, BACKGROUND_FADE_DURATION, Easing.OutQuint);
            }
        }
    }
}
