// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public static class ManiaEzSkinEditorNoteDrawablePreviewBuilder
    {
        private const int preview_key_count = 4;

        public static Drawable Create(ISkin skin, EzSkinEditorNotePreviewRequest request)
        {
            bool isHold = request.CompareKind == EzSkinEditorNoteCompareKind.Hold;
            var transformedSkin = createTransformedSkin(skin);
            double holdDuration = Math.Clamp(request.Height * 0.85, 48, 512);

            ManiaHitObject hitObject = isHold
                ? new HoldNote { StartTime = 0, Duration = holdDuration, Column = 0 }
                : new Note { StartTime = 0, Column = 0 };

            hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            DrawableHitObject hitDrawable = isHold
                ? new DrawableHoldNote((HoldNote)hitObject)
                : new DrawableNote((Note)hitObject);

            // Comparison panes are hitbox-sized; EzNoteContainer's fixed column layout pushes content off-screen.
            return new SkinProvidingContainer(transformedSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new EzSkinLNEditorProvider.PreviewDependencyContainer(preview_key_count, 0, ManiaAction.Key1)
                {
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = hitDrawable.With(d =>
                        {
                            d.Anchor = Anchor.Centre;
                            d.Origin = Anchor.Centre;
                            d.RelativeSizeAxes = Axes.X;
                            d.Width = 1;
                        }),
                    },
                },
            };
        }

        private static ISkin createTransformedSkin(ISkin skin)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new ManiaBeatmap(new StageDefinition(preview_key_count));
            return ruleset.CreateSkinTransformer(skin, beatmap) ?? skin;
        }
    }
}
