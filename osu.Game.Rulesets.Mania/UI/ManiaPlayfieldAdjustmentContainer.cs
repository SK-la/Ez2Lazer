// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class ManiaPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        protected override Container<Drawable> Content { get; }

        private readonly DrawSizePreservingFillContainer scalingContainer;
        private readonly Container positionContainer;
        private readonly BufferedContainer? perspectiveContainer;

        private readonly DrawableManiaRuleset drawableManiaRuleset;
        private Bindable<double> maniaPseudo3DRotation = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private ManiaPlayfieldLayoutConfig playfieldLayout { get; set; } = null!;

        private Anchor appliedAnchor;
        private Vector2 appliedPosition;

        public ManiaPlayfieldAdjustmentContainer(DrawableManiaRuleset drawableManiaRuleset)
        {
            this.drawableManiaRuleset = drawableManiaRuleset;

            InternalChild = scalingContainer = new DrawSizePreservingFillContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };

            Content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            // The stage position is applied here, outside the pseudo-3D buffer, so an offset never clips against it.
            positionContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };

            var rotation = GlobalConfigStore.EzConfig.GetBindable<double>(Ez2Setting.ManiaPseudo3DRotation);

            if (!rotation.IsDefault)
            {
                positionContainer.Child = perspectiveContainer = new BufferedContainer(pixelSnapping: true)
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    DrawOriginal = false,
                    Child = Content
                };
            }
            else
            {
                positionContainer.Child = Content;
            }

            scalingContainer.Child = positionContainer;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            maniaPseudo3DRotation = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaPseudo3DRotation);
            maniaPseudo3DRotation.BindValueChanged(_ => updatePerspective(), true);
        }

        private void updatePerspective()
        {
            float angle = (float)maniaPseudo3DRotation.Value;
            float t = angle / 75f;

            if (perspectiveContainer == null) return;

            if (t <= 0)
            {
                perspectiveContainer.VerticalPerspective = 0;
                return;
            }

            perspectiveContainer.VerticalPerspective = t;
            perspectiveContainer.PerspectiveScale = new Vector2(1.0f - t * 0.9f, 1.0f - t * 0.8f);
            perspectiveContainer.PerspectiveVerticalOffset = 0.0f;
        }

        protected override void Update()
        {
            base.Update();

            float aspectRatio = DrawWidth / DrawHeight;
            bool isPortrait = aspectRatio < 1f;

            if (isPortrait && drawableManiaRuleset.Beatmap.Stages.Count == 1)
            {
                // Scale playfield up by 25% to become playable on mobile devices,
                // and leave a 10% horizontal gap if the playfield is scaled down due to being too wide.
                const float base_scale = 1.25f;
                const float base_width = 768f / base_scale;
                const float side_gap = 0.9f;

                scalingContainer.Strategy = DrawSizePreservationStrategy.Maximum;
                float stageWidth = drawableManiaRuleset.Playfield.Stages[0].DrawWidth;
                scalingContainer.TargetDrawSize = new Vector2(1024, base_width * Math.Max(stageWidth / aspectRatio / (base_width * side_gap), 1f));
            }
            else
            {
                scalingContainer.Strategy = DrawSizePreservationStrategy.Minimum;
                scalingContainer.Scale = new Vector2(1f);
                scalingContainer.Size = new Vector2(1f);
                scalingContainer.TargetDrawSize = new Vector2(1024, 768);
            }

            updateStagePosition();
        }

        private void updateStagePosition()
        {
            float containerWidth = positionContainer.DrawWidth;
            float containerHeight = positionContainer.DrawHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            float stageWidth = 0;

            foreach (var stage in drawableManiaRuleset.Playfield.Stages)
                stageWidth += stage.DrawWidth;

            float value = playfieldLayout.StageXFraction;
            int direction = ManiaPlayfieldLayoutHelper.GetStageHorizontalDirection(value);

            playfieldLayout.StageOffsetFractionX = ManiaPlayfieldLayoutHelper.CalculateStageOffsetFraction(value, containerWidth, stageWidth);

            Anchor anchor = direction < 0 ? Anchor.CentreLeft : direction > 0 ? Anchor.CentreRight : Anchor.Centre;
            Vector2 position = ManiaPlayfieldLayoutHelper.CalculateStageOffsetPosition(
                value,
                playfieldLayout.StageYFraction,
                direction,
                containerWidth,
                containerHeight,
                stageWidth);

            if (appliedAnchor == anchor && appliedPosition == position)
                return;

            appliedAnchor = anchor;
            appliedPosition = position;
            positionContainer.Anchor = anchor;
            positionContainer.Position = position;
        }
    }
}
