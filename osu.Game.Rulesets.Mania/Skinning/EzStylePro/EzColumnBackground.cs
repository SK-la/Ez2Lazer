// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private Bindable<double> hitPosition = new Bindable<double>();
        private Color4 brightColour;
        private Color4 dimColour;

        private Box backgroundOverlay = null!;

        private readonly Box separator = new Box
        {
            Name = "Separator",
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopCentre,
            Width = 2,
            Colour = Color4.White.Opacity(0.5f),
            Alpha = 0,
        };

        private Bindable<Color4> accentColour = null!;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzColumnBackground()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
            // Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Name = "Background",
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.8f).Darken(3),
                    },
                    backgroundOverlay = new Box
                    {
                        Name = "Background Gradient Overlay",
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Height = 0.5f,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0
                    },
                }
            };

            accentColour = new Bindable<Color4>(ezSkinConfig.GetColumnColor(stageDefinition.Columns, Column.Index));
            accentColour.BindValueChanged(colour =>
            {
                var newColour = colour.NewValue.Darken(3);

                if (newColour.A != 0)
                {
                    newColour = newColour.Opacity(0.8f);
                }

                backgroundOverlay.Colour = newColour;
                brightColour = colour.NewValue.Opacity(0.6f);
                dimColour = colour.NewValue.Opacity(0);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bool hasSeparator = Column.TopLevelContainer.Children
                                      .OfType<Box>()
                                      .Any(b => b.Name == "Separator");

            if (!hasSeparator)
                Column.TopLevelContainer.Add(separator);

            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            hitPosition.BindValueChanged(_ => OnConfigChanged(), true);
        }

        protected virtual Color4 NoteColor
        {
            get
            {
                int keyMode = stageDefinition.Columns;
                int columnIndex = Column.Index;
                return ezSkinConfig.GetColumnColor(keyMode, columnIndex);
            }
        }

        private void OnConfigChanged()
        {
            separator.Height = DrawHeight - (float)hitPosition.Value;

            if (drawSeparator(Column.Index, stageDefinition))
                separator.Alpha = 0.25f;
            else
                separator.Alpha = 0;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
            {
                var noteColour = NoteColor;
                brightColour = noteColour.Opacity(0.9f);
                dimColour = noteColour.Opacity(0);
                backgroundOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);
                backgroundOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
                backgroundOverlay.FadeTo(0, 250, Easing.OutQuint);
        }

        //TODO: 这里的逻辑可以优化，避免重复计算
        private bool drawSeparator(int columnIndex, StageDefinition stage)
        {
            return stage.Columns switch
            {
                12 => columnIndex is 0 or 10,
                14 => columnIndex is 0 or 5 or 6 or 11,
                16 => columnIndex is 0 or 5 or 9 or 14,
                _ => false
            };
        }
    }
}
