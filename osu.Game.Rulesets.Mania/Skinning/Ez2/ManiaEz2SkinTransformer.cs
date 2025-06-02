// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public class ManiaEz2SkinTransformer : SkinTransformer
    {
        private readonly ManiaBeatmap beatmap;

        // private readonly OsuConfigManager config;
        // private readonly EzSkinSettingsTab ezSkinSettings;
        // private readonly float totalColumnWidth;
        private readonly IBindable<double> columnWidthBindable;
        private readonly IBindable<double> specialFactorBindable;

        private readonly float hitPosition;

        private float columnWidth { get; set; }

        //EzSkinSettings即使不用也不能删，否则特殊列计算会出错
        public ManiaEz2SkinTransformer(ISkin skin, IBeatmap beatmap, OsuConfigManager config, EzSkinSettingsManager ezSkinConfig)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;

            // this.config = config ?? throw new ArgumentNullException(nameof(config));
            // this.ezSkinSettings = ezSkinSettings ?? throw new ArgumentNullException(nameof(ezSkinSettings));

            columnWidthBindable = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactorBindable = config.GetBindable<double>(OsuSetting.SpecialFactor);
            IBindable<double> virtualHitPositionBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.VirtualHitPosition);
            hitPosition = (float)virtualHitPositionBindable.Value;
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
                                var hitTiming = container.ChildrenOfType<EzComHitTiming>().ToArray();

                                if (hitTiming.Length >= 2)
                                {
                                    var hitTiming1 = hitTiming[0];
                                    var hitTiming2 = hitTiming[1];
                                    const float mirror_x = 500;

                                    hitTiming1.Anchor = Anchor.Centre;
                                    hitTiming1.Origin = Anchor.Centre;
                                    hitTiming1.X = -mirror_x;
                                    // hitTiming1.Scale = new Vector2(2);
                                    hitTiming1.AloneShow.Value = AloneShowMenu.Early;

                                    hitTiming2.Anchor = Anchor.Centre;
                                    hitTiming2.Origin = Anchor.Centre;
                                    hitTiming2.X = mirror_x;
                                    // hitTiming2.Scale = new Vector2(2);
                                    hitTiming2.AloneShow.Value = AloneShowMenu.Late;
                                }

                                var comboSprite = container.ChildrenOfType<EzComComboSprite>().FirstOrDefault();

                                if (comboSprite != null)
                                {
                                    comboSprite.Anchor = Anchor.TopCentre;
                                    comboSprite.Origin = Anchor.Centre;
                                    comboSprite.Y = 190;
                                }

                                var combos = container.ChildrenOfType<EzComComboCounter>().ToArray();

                                if (combos.Length >= 2)
                                {
                                    var combo1 = combos[0];
                                    var combo2 = combos[1];

                                    combo1.Anchor = Anchor.TopCentre;
                                    combo1.Origin = Anchor.TopCentre;
                                    combo1.Y = 200;
                                    combo1.BoxAlpha.Value = 0.8f;
                                    combo1.EffectStartFactor.Value = 1.5f;
                                    combo1.EffectEndFactor.Value = 1f;
                                    combo1.EffectStartTime.Value = 10;
                                    combo1.EffectEndDuration.Value = 500;

                                    combo2.Anchor = Anchor.TopCentre;
                                    combo2.Origin = Anchor.TopCentre;
                                    combo2.Y = 200;
                                    combo2.BoxAlpha.Value = 0.4f;
                                    combo2.EffectStartFactor.Value = 3f;
                                    combo2.EffectEndFactor.Value = 1f;
                                    combo2.EffectStartTime.Value = 10;
                                    combo2.EffectEndDuration.Value = 300;
                                }

                                var keyCounter = container.ChildrenOfType<EzComKeyCounterDisplay>().FirstOrDefault();
                                var columnHitErrorMeter = container.OfType<EzComHitTimingColumns>().FirstOrDefault();

                                if (keyCounter != null)
                                {
                                    keyCounter.Anchor = Anchor.BottomCentre;
                                    keyCounter.Origin = Anchor.TopCentre;
                                    keyCounter.Position = new Vector2(0, -hitPosition - stage_padding_bottom);
                                }

                                if (columnHitErrorMeter != null)
                                {
                                    columnHitErrorMeter.Anchor = Anchor.BottomCentre;
                                    columnHitErrorMeter.Origin = Anchor.Centre;
                                    columnHitErrorMeter.Position = new Vector2(0, -hitPosition - stage_padding_bottom);
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

                                var judgementPiece = container.OfType<EzComHitResultScore>().FirstOrDefault();

                                if (judgementPiece != null)
                                {
                                    judgementPiece.Anchor = Anchor.Centre;
                                    judgementPiece.Origin = Anchor.Centre;
                                    judgementPiece.Y = 50;
                                }
                            })
                            {
                                new EzComComboSprite(),
                                new EzComComboCounter(),
                                new EzComComboCounter(),
                                new EzComKeyCounterDisplay(),
                                new EzComHitTimingColumns(),
                                new BarHitErrorMeter(),
                                new EzComHitResultScore(),
                                new EzComHitTiming(),
                                new EzComHitTiming(),
                            };
                    }

                    return null;

                case SkinComponentLookup<HitResult>:
                    // if (Skin is Ez2Skin && resultComponent.Component > HitResult.Great)
                    //     return Drawable.Empty();

                    // return new Ez2JudgementPiece(resultComponent.Component);
                    return Drawable.Empty();

                case ManiaSkinComponentLookup maniaComponent:
                    switch (maniaComponent.Component)
                    {
                        case ManiaSkinComponents.StageBackground:
                            return new Ez2StageBackground();

                        case ManiaSkinComponents.ColumnBackground:
                            // if (Skin is Ez2Skin && resultComponent.Component >= HitResult.Perfect)
                            //     return Drawable.Empty();

                            return new Ez2ColumnBackground();

                        case ManiaSkinComponents.KeyArea:
                            return new Ez2KeyArea();

                        case ManiaSkinComponents.HitTarget:
                            return new Ez2HitTarget();

                        case ManiaSkinComponents.Note:
                            return new Ez2NotePiece();

                        case ManiaSkinComponents.HitExplosion:
                            return new Ez2HitExplosion();

                        case ManiaSkinComponents.HoldNoteTail:
                            return new Ez2HoldNoteTailPiece();

                        case ManiaSkinComponents.HoldNoteBody:
                            return new Ez2HoldBodyPiece();

                        case ManiaSkinComponents.HoldNoteHead:
                            return new Ez2HoldNoteHeadPiece();
                    }

                    break;
            }

            return base.GetDrawableComponent(lookup);
        }

        private const int stage_padding_bottom = 0;

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                int columnIndex = maniaLookup.ColumnIndex ?? 0;
                var stage = beatmap.GetStageForColumnIndex(columnIndex);
                bool isSpecialColumn = stage.EzIsSpecialColumn(columnIndex);

                // float columnWidth = (float)config.Get<double>(OsuSetting.ColumnWidth);
                // float specialFactor = (float)config.Get<double>(OsuSetting.SpecialFactor);
                // float width = columnWidth * (isSpecialColumn ? specialFactor : 1);
                columnWidth = (float)columnWidthBindable.Value * (isSpecialColumn ? (float)specialFactorBindable.Value : 1);

                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnWidth:
                        return SkinUtils.As<TValue>(new Bindable<float>(columnWidth));

                    case LegacyManiaSkinConfigurationLookups.HitPosition:
                        return SkinUtils.As<TValue>(new Bindable<float>(hitPosition));

                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:

                        var colour = stage.GetColourForLayout(columnIndex);

                        return SkinUtils.As<TValue>(new Bindable<Color4>(colour));

                    case LegacyManiaSkinConfigurationLookups.BarLineHeight:
                        return SkinUtils.As<TValue>(new Bindable<float>(1));

                    case LegacyManiaSkinConfigurationLookups.LeftColumnSpacing:
                    case LegacyManiaSkinConfigurationLookups.RightColumnSpacing:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingBottom:
                        return SkinUtils.As<TValue>(new Bindable<float>(stage_padding_bottom));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingTop:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }
    }
}
