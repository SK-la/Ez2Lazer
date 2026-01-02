// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;
using osu.Framework.Bindables;
using System.Linq;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    /// <summary>
    /// HUD 组件：显示 Pills（💊）图标数量，并提供两个下拉选项：Pill 精灵图选择 和 排列方向（横向/纵向）。
    /// 外部负责将游戏中的 PillCount 同步到此控件的 `UpdatePillCount(int)`。
    /// </summary>
    public partial class EzComO2JamPillUI : CompositeDrawable, ISerialisableDrawable
    {
        public enum PillSprite
        {
            CheckCircle,
            Heart,
            Star,
            ThumbsUp,
            ModSuddenDeath,
        }

        [SettingSource("Pill Sprite", "(药丸图)Pill Sprite")]
        public Bindable<PillSprite> SpriteDropdown { get; } = new Bindable<PillSprite>(PillSprite.CheckCircle);

        [SettingSource("Pill Direction", "(药丸方向)Pill Direction")]
        public Bindable<FillDirection> PillFillDirection { get; } = new Bindable<FillDirection>(FillDirection.Vertical);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CornerRadius), nameof(SkinnableComponentStrings.CornerRadiusDescription),
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0.25f)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f
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

        public EzComO2JamPillUI()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                // 半透明背景底框
                backgroundContainer = new Container
                {
                    Size = new Vector2(60, 280), // 默认垂直形状
                    Masking = true,
                    CornerRadius = 8,
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
                        }
                    }
                },
                pillContainer = new FillFlowContainer
                {
                    Name = "pills",
                    RelativeSizeAxes = Axes.Both,
                    Margin = new MarginPadding(5),
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    // Children = new Drawable[]
                    // {
                    //     new OsuSpriteText
                    //     {
                    //         Text = "💊",
                    //         Font = new FontUsage(null, 40),
                    //     }
                    // }
                }
            };

            SpriteDropdown.BindValueChanged(_ => rebuildPills());
            PillFillDirection.BindValueChanged(_ => updateLayout());
            PillCount.BindValueChanged(value =>
            {
                currentPillCount = value.NewValue;
                rebuildPills();
            });
        }

        private void updateLayout()
        {
            pillContainer.Direction = PillFillDirection.Value;
            backgroundContainer.Size = PillFillDirection.Value == FillDirection.Vertical
                ? new Vector2(60, 280)
                : new Vector2(280, 60);
            rebuildPills();
        }

        private void rebuildPills()
        {
            pillContainer.Clear();

            for (int i = 0; i < currentPillCount; i++)
            {
                pillContainer.Add(
                    new SpriteIcon
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Size = new Vector2(50, 50),
                        Icon = pill_sprites[(int)SpriteDropdown.Value],
                        Colour = Color4.White
                    });
                    // new OsuSpriteText
                    // {
                    //     Text = "💊",
                    //     Font = new FontUsage(null, 40),
                    // });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            PillCount.BindTo(O2HitModeExtension.PillCount);
            PillCount = O2HitModeExtension.PillCount;
            AccentColour.BindValueChanged(_ => Colour = AccentColour.Value, true);
            rebuildPills();
        }

        protected override void Update()
        {
            base.Update();

            base.CornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
