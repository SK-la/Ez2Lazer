// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    /// <summary>
    /// HUD 组件：显示 Pills（💊）图标数量，并提供两个下拉选项：Pill 精灵图选择 和 排列方向（横向/纵向）。
    /// 外部负责将游戏中的 PillCount 同步到此控件的 `UpdatePillCount(int)`。
    /// </summary>
    public partial class EzHUDO2JamPillFlow : CompositeDrawable, ISerialisableDrawable
    {
        private const int max_pill_count = 5;
        private const float pill_slot_size = 48;
        private const float pill_spacing = 6;
        private const float background_padding = 8;

        public enum PillSprite
        {
            Pills = 0, // 以后需要考虑彩色
            CheckCircle = 1,
            Heart = 2,
            Star = 3,
            ThumbsUp = 4,
            ModSuddenDeath = 5,
        }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.PILL_SPRITE_LABEL), nameof(EzHUDStrings.PILL_SPRITE_DESCRIPTION))]
        public Bindable<PillSprite> SpriteDropdown { get; } = new Bindable<PillSprite>(PillSprite.Pills);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.FILL_DIRECTION_LABEL), nameof(EzHUDStrings.FILL_DIRECTION_DESCRIPTION))]
        public Bindable<FillDirection> PillFillDirection { get; } = new Bindable<FillDirection>(FillDirection.Vertical);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CornerRadius), nameof(SkinnableComponentStrings.CornerRadiusDescription),
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0.25f)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BACKGROUND_ALPHA_LABEL), nameof(EzHUDStrings.BOX_ELEMENT_ALPHA_DESCRIPTION))]
        public BindableNumber<float> BackgroundAlpha { get; } = new BindableNumber<float>(0.7f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        // 可配置的精灵表（使用 osu 图标系统）
        private static readonly IconUsage[] pill_sprites = new[]
        {
            OsuIcon.CheckCircle,
            OsuIcon.Heart,
            OsuIcon.Star,
            OsuIcon.ThumbsUp,
            OsuIcon.ModSuddenDeath,
        };

        public Bindable<int> PillCount { get; set; } = new Bindable<int>();

        private int currentPillCount;

        private FillFlowContainer pillContainer = null!;

        private Container backgroundContainer = null!;

        private bool rebuildScheduled;
        private bool layoutScheduled;

        public EzHUDO2JamPillFlow()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;

            InternalChild = backgroundContainer = new Container
            {
                Masking = true,
                CornerRadius = 8,
                Alpha = BackgroundAlpha.Value,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.7f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0.3f,
                    },
                    pillContainer = new FillFlowContainer
                    {
                        Name = "pills",
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(background_padding),
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(pill_spacing),
                    }
                }
            };

            updateBackgroundSize();

            BackgroundAlpha.BindValueChanged(value => requestAlphaUpdate(value.NewValue), true);
            SpriteDropdown.BindValueChanged(_ => requestRebuild(), true);
            PillFillDirection.BindValueChanged(_ => requestLayoutUpdate(), true);
        }

        private void requestAlphaUpdate(float alpha)
        {
            // Mutations must occur on the update thread.
            Schedule(() =>
            {
                if (IsDisposed)
                    return;

                backgroundContainer.Alpha = alpha;
            });
        }

        private void requestLayoutUpdate()
        {
            if (layoutScheduled)
                return;

            layoutScheduled = true;

            Schedule(() =>
            {
                layoutScheduled = false;

                if (IsDisposed)
                    return;

                pillContainer.Direction = PillFillDirection.Value;
                updateBackgroundSize();

                requestRebuild();
            });
        }

        private void updateBackgroundSize()
        {
            const float main_axis = max_pill_count * pill_slot_size + (max_pill_count - 1) * pill_spacing;
            const float cross_axis = pill_slot_size;

            backgroundContainer.Size = PillFillDirection.Value == FillDirection.Vertical
                ? new Vector2(cross_axis, main_axis) + new Vector2(background_padding * 2)
                : new Vector2(main_axis, cross_axis) + new Vector2(background_padding * 2);
        }

        private void requestRebuild()
        {
            if (rebuildScheduled)
                return;

            rebuildScheduled = true;

            Schedule(() =>
            {
                rebuildScheduled = false;

                if (IsDisposed)
                    return;

                rebuildPills();
            });
        }

        private void rebuildPills()
        {
            pillContainer.Clear();

            for (int i = 0; i < currentPillCount; i++)
                pillContainer.Add(createPillDrawable());
        }

        private Drawable createPillDrawable()
        {
            Drawable iconDrawable;

            if (SpriteDropdown.Value == PillSprite.Pills)
            {
                iconDrawable = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "💊",
                    Font = new FontUsage(null, 46),
                };
            }
            else
            {
                iconDrawable = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(pill_slot_size),
                    Icon = pill_sprites[(int)SpriteDropdown.Value],
                    Colour = Color4.White
                };
            }

            return new Container
            {
                Size = new Vector2(pill_slot_size),
                Child = iconDrawable
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            PillCount.BindTo(O2HitModeExtension.PILL_COUNT);

            PillCount.BindValueChanged(value =>
            {
                currentPillCount = value.NewValue;
                // Logger.Log($"[EzComO2JamPillUI] PillCount changed -> {currentPillCount}");
                requestRebuild();
            }, true);

            AccentColour.BindValueChanged(_ => Colour = AccentColour.Value, true);
        }

        protected override void Update()
        {
            base.Update();

            base.CornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
