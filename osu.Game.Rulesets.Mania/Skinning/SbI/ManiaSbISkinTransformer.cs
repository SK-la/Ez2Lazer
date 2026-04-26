// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Ez2HUD;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    /// <summary>
    /// 本皮肤原则上应专注极致性能。
    /// </summary>
    public class ManiaSbISkinTransformer : SkinTransformer
    {
        private readonly ManiaBeatmap beatmap;
        private readonly Ez2ConfigManager ezSkinConfig;
        private readonly float hitPosition;

        public ManiaSbISkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;

            ezSkinConfig = GlobalConfigStore.EzConfig;
            hitPosition = (float)ezSkinConfig.Get<double>(Ez2Setting.HitPosition);
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
                                var hitTiming = container.ChildrenOfType<EzHUDHitTiming>().ToArray();

                                if (hitTiming.Length >= 2)
                                {
                                    var hitTiming1 = hitTiming[0];
                                    var hitTiming2 = hitTiming[1];
                                    const float mirror_x = 350;

                                    hitTiming1.Anchor = Anchor.Centre;
                                    hitTiming1.Origin = Anchor.Centre;
                                    hitTiming1.DisplayDuration.Value = hitTiming1.DisplayDuration.MinValue;
                                    hitTiming1.X = -mirror_x;
                                    // hitTiming1.Scale = new Vector2(2);
                                    hitTiming1.AloneShow.Value = AloneShowMenu.Early;

                                    hitTiming2.Anchor = Anchor.Centre;
                                    hitTiming2.Origin = Anchor.Centre;
                                    hitTiming2.DisplayDuration.Value = hitTiming2.DisplayDuration.MinValue;
                                    hitTiming2.X = mirror_x;
                                    // hitTiming2.Scale = new Vector2(2);
                                    hitTiming2.AloneShow.Value = AloneShowMenu.Late;
                                }

                                var combo1 = container.OfType<EzHUDComboCounter>().FirstOrDefault();

                                if (combo1 != null)
                                {
                                    combo1.Anchor = Anchor.TopCentre;
                                    combo1.Origin = Anchor.Centre;
                                    combo1.Y = 200;
                                    combo1.Effect.Value = EzComEffectType.None;
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
                                    hitErrorMeter.JudgementFadeOutDuration.Value = hitErrorMeter.JudgementFadeOutDuration.MinValue;
                                    hitErrorMeter.ShowMovingAverage.Value = false;
                                    hitErrorMeter.ColourBarVisibility.Value = false;
                                    hitErrorMeter.CentreMarkerStyle.Value = BarHitErrorMeter.CentreMarkerStyles.Line;
                                    hitErrorMeter.LabelStyle.Value = BarHitErrorMeter.LabelStyles.None;
                                }

                                _ = container.OfType<YuComFastSlowDisplay>().FirstOrDefault();
                            })
                            {
                                new EzHUDHitTiming(),
                                new EzHUDHitTiming(),
                                new EzHUDComboCounter(),
                                new BarHitErrorMeter(),
                            };
                    }

                    return null;

                case SkinComponentLookup<HitResult>:
                    // if (Skin is SbISkin && resultComponent.Component >= HitResult.Great)
                    //     return Drawable.Empty();
                    // return new SbIJudgementPiece(resultComponent.Component);
                    return Drawable.Empty();

                case ManiaSkinComponentLookup maniaComponent:
                    switch (maniaComponent.Component)
                    {
                        case ManiaSkinComponents.ColumnBackground:
                            return new SbIColumnBackground();

                        case ManiaSkinComponents.KeyArea:
                            return Drawable.Empty();

                        case ManiaSkinComponents.HoldNoteHead:
                            return Drawable.Empty();

                        case ManiaSkinComponents.HoldNoteTail:
                            return new SbIHoldNoteTailPiece();

                        case ManiaSkinComponents.HoldNoteBody:
                            return new SbIHoldBodyPiece();

                        case ManiaSkinComponents.Note:
                            return new SbINotePiece();

                        case ManiaSkinComponents.HitTarget:
                            return new SbIHitTarget();

                        case ManiaSkinComponents.HitExplosion:
                            return Drawable.Empty();

                        case ManiaSkinComponents.StageBackground:
                            //return new SbIStageBackground();
                            return Drawable.Empty();
                    }

                    break;
            }

            return base.GetDrawableComponent(lookup);
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                int keyMode = beatmap.TotalColumns;
                int columnIndex = maniaLookup.ColumnIndex ?? 0;
                float columnWidth = ezSkinConfig.GetColumnWidth(keyMode, columnIndex);

                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnWidth:
                        return SkinUtils.As<TValue>(new Bindable<float>(columnWidth));

                    case LegacyManiaSkinConfigurationLookups.HitPosition:
                        return SkinUtils.As<TValue>(new Bindable<float>(hitPosition));

                    case LegacyManiaSkinConfigurationLookups.BarLineHeight:
                        return SkinUtils.As<TValue>(new Bindable<float>());

                    case LegacyManiaSkinConfigurationLookups.LeftColumnSpacing:
                    case LegacyManiaSkinConfigurationLookups.RightColumnSpacing:
                        return SkinUtils.As<TValue>(new Bindable<float>());

                    case LegacyManiaSkinConfigurationLookups.StagePaddingBottom:
                        return SkinUtils.As<TValue>(new Bindable<float>());

                    case LegacyManiaSkinConfigurationLookups.StagePaddingTop:
                        return SkinUtils.As<TValue>(new Bindable<float>());

                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:

                        var colour = Colour4.White;

                        return SkinUtils.As<TValue>(new Bindable<Color4>(colour));
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }
    }
}
