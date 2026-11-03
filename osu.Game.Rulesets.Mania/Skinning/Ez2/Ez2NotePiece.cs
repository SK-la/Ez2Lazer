// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2NotePiece : FastNoteBase
    {
        public static float NoteHeight = 45;
        public const float NOTE_ACCENT_RATIO = 1f;
        public const float CORNER_RADIUS = 0;

        private Circle colouredBox = null!;
        private readonly Drawable iconContainer;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        // 10k2s1p 开启时用 Ez 内置色彩模板，关闭时回到 FastNoteBase 的编辑器列色。
        protected override IBindable<bool>? BuiltInColourTemplateSwitch => ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);

        protected override Colour4 BuiltInColourTemplate => stageDefinition.GetColourForLayout(Column.Index);

        public Ez2NotePiece()
        {
            RelativeSizeAxes = Axes.X;

            CornerRadius = CORNER_RADIUS;
            // Masking = true;

            iconContainer = CreateIcon();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            MainContainer.Children = new[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Circle
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        BorderThickness = 4,
                        // BorderColour = Color4.White.Opacity(1f),
                        // BorderColour = ColourInfo.GradientVertical(Color4.White.Opacity(0), Colour4.Black),
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Colour = Colour4.Black.Opacity(1f),
                            Radius = 1, // 调整描边的宽度
                            Roundness = 0 // 调整描边的圆角程度
                        }
                    }
                },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    // Masking = true,
                    // CornerRadius = CORNER_RADIUS,
                    Children = new Drawable[]
                    {
                        colouredBox = new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            BorderThickness = 4,
                            BorderColour = Colour4.White.Opacity(0.7f),
                            // BorderThickness = 2,
                            // Alpha = 0.5f,
                            //Blending = BlendingParameters.Additive,
                        }
                    }
                },
                new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                },
                iconContainer,
            };
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;
            // NoteHeight = columnWidth;
            // NoteHeight = (float)config.Get<double>(ManiaRulesetSetting.ColumnWidth);
            // specialFactor = (float)config.Get<double>(ManiaRulesetSetting.SpecialFactor);

            iconContainer.Size = new Vector2(DrawWidth / NoteHeight * 0.7f);
        }

        protected virtual Drawable CreateIcon() => new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(80),
            Y = 0,
            Children = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.3f),
                    Masking = true,
                    Child = new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.5f),
                        EdgeEffect = new EdgeEffectParameters()
                    }
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.Circle,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.225f),
                    Colour = Colour4.White.Opacity(0.8f),
                }
            }
        };

        protected override void UpdateColor()
        {
            colouredBox.Colour = ColourInfo.GradientVertical(
                NoteColor.Lighten(0.1f),
                NoteColor
            );
        }
    }
}
