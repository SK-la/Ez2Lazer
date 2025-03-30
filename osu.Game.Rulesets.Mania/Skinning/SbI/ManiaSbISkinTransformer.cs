// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Ez2;
using osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public class ManiaSbISkinTransformer : SkinTransformer
    {
        private readonly ManiaBeatmap beatmap;

        public ManiaSbISkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            switch (lookup)
            {
                case GlobalSkinnableContainerLookup containerLookup:
                    if (containerLookup.Ruleset == null)
                        return base.GetDrawableComponent(lookup);

                    switch (containerLookup.Lookup)
                    {
                        case GlobalSkinnableContainers.MainHUDComponents:
                            return new DefaultSkinComponentsContainer(container =>
                            {
                                var combo1 = container.OfType<EzComComboCounter>().FirstOrDefault();

                                if (combo1 != null)
                                {
                                    combo1.Anchor = Anchor.TopCentre;
                                    combo1.Origin = Anchor.Centre;
                                    combo1.Y = 200;
                                    combo1.ShowLabel.Value = false;
                                    combo1.Alpha = 1f;
                                    combo1.Current.BindValueChanged(changedEvent =>
                                    {
                                        bool wasIncrease = changedEvent.NewValue > changedEvent.OldValue;
                                        bool wasMiss = changedEvent.OldValue > 1 && changedEvent.NewValue == 0;

                                        if (wasIncrease)
                                        {
                                            combo1.Text.NumberContainer
                                                  .ScaleTo(new Vector2(1.5f, 1.5f), 10, Easing.OutQuint)
                                                  .Then()
                                                  .ScaleTo(Vector2.One, 500, Easing.OutQuint);
                                        }

                                        if (wasMiss)
                                        {
                                            combo1.Text.FlashColour(Color4.Red, 500, Easing.OutQuint);
                                        }
                                    });
                                }

                                var hitErrorMeter = container.OfType<BarHitErrorMeter>().FirstOrDefault();

                                if (hitErrorMeter != null)
                                {
                                    hitErrorMeter.Anchor = Anchor.Centre;
                                    hitErrorMeter.Origin = Anchor.Centre;
                                    hitErrorMeter.Rotation = -90f;
                                    hitErrorMeter.Position = new Vector2(0, -15);
                                    hitErrorMeter.Scale = new Vector2(1.4f, 1.4f);
                                    hitErrorMeter.JudgementLineThickness.Value = 2;
                                    hitErrorMeter.ShowMovingAverage.Value = true;
                                    hitErrorMeter.ColourBarVisibility.Value = false;
                                    hitErrorMeter.CentreMarkerStyle.Value = BarHitErrorMeter.CentreMarkerStyles.Circle;
                                    hitErrorMeter.LabelStyle.Value = BarHitErrorMeter.LabelStyles.None;
                                }
                            })
                            {
                                new EzComComboCounter(),
                                new BarHitErrorMeter(),
                            };
                    }

                    return null;

                case SkinComponentLookup<HitResult> resultComponent:
                    // if (Skin is SbISkin && resultComponent.Component >= HitResult.Great)
                    //     return Drawable.Empty();
                    return new GifJudgementPiece(resultComponent.Component);
                    // return new SbIJudgementPiece(resultComponent.Component);

                case ManiaSkinComponentLookup maniaComponent:
                    switch (maniaComponent.Component)
                    {
                        // case ManiaSkinComponents.StageBackground:
                        //     return new SbIStageBackground();

                        case ManiaSkinComponents.ColumnBackground:
                            // if (Skin is SbISkin && resultComponent.Component >= HitResult.Perfect)
                            //     return Drawable.Empty();
                            return new SbIColumnBackground();

                        case ManiaSkinComponents.HoldNoteBody:
                            return new SbIHoldBodyPiece();

                        case ManiaSkinComponents.HoldNoteTail:
                            return new SbIHoldNoteTailPiece();

                        case ManiaSkinComponents.HoldNoteHead:
                            return new SbIHoldNoteHeadPiece();

                        case ManiaSkinComponents.Note:
                            return new SbINotePiece();

                        // case ManiaSkinComponents.HitTarget:
                        //     return new SbIHitTarget();

                        case ManiaSkinComponents.KeyArea:
                            return new SbIKeyArea();

                        // case ManiaSkinComponents.HitExplosion:
                            // return new SbIHitExplosion();
                    }

                    break;
            }

            return base.GetDrawableComponent(lookup);
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                int columnIndex = maniaLookup.ColumnIndex ?? 0;
                var stage = beatmap.GetStageForColumnIndex(columnIndex);

                const float total_width = 512;
                float width = total_width / stage.Columns;

                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnSpacing:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingBottom:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingTop:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.HitPosition:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.ColumnWidth:

                        return SkinUtils.As<TValue>(new Bindable<float>(width));

                    case LegacyManiaSkinConfigurationLookups.BarLineHeight:
                        return SkinUtils.As<TValue>(new Bindable<int>(0));

                    case LegacyManiaSkinConfigurationLookups.BarLineColour:
                        return SkinUtils.As<TValue>(new Bindable<Color4>(Color4.Transparent));

                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:

                        var colour = Colour4.White;

                        return SkinUtils.As<TValue>(new Bindable<Color4>(colour));
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }
    }
}
