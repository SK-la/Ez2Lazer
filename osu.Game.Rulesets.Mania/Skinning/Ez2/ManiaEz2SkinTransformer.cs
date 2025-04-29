// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public class ManiaEz2SkinTransformer : SkinTransformer
    {
        private readonly ManiaBeatmap beatmap;

        // private readonly Bindable<float> columnWidth = new Bindable<float>(50);
        // private readonly Bindable<float> specialFactor = new Bindable<float>(1);

        private readonly OsuConfigManager config;
        // private readonly ManiaRulesetConfigManager config;

        // private readonly float totalColumnWidth;
        public ManiaEz2SkinTransformer(ISkin skin, IBeatmap beatmap, OsuConfigManager config)
            : base(skin)
        {
            this.beatmap = (ManiaBeatmap)beatmap;

            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // [BackgroundDependencyLoader]
        // private void load(ManiaRulesetConfigManager config)
        // {
        //     // 将配置中的绑定值转换后绑定到我们自己的绑定变量上
        //     // 假设你的 ManiaRulesetConfigManager 提供了一个 GetBindable<double> 方法：
        //     columnWidth.BindTo(config.GetBindable<double>(ManiaRulesetSetting.ColumnWidth).ConvertToFloatBindable());
        // }

        // public ManiaEz2SkinTransformer(ISkin skin, IBeatmap beatmap)
        //     : base(skin)
        // {
        //     this.beatmap = (ManiaBeatmap)beatmap;

            // if (this.beatmap.TotalColumns <= 10)
            // {
            // totalColumnWidth = 0.82f;
            // }
            // else
            // {
            //     totalColumnWidth = 7 * 40f / 200f;
            // }
        // }

        // private void calculateColumnWidth(int columnIndex, StageDefinition stage)
        // {
        //     bool isSpecialColumn = stage.IsSpecialColumn(columnIndex);
        //     width = (float)(46 * (isSpecialColumn ? 1.2 : 1));
        // }

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

                                if (keyCounter != null)
                                {
                                    keyCounter.Anchor = Anchor.BottomCentre;
                                    keyCounter.Origin = Anchor.TopCentre;
                                    keyCounter.Position = new Vector2(0, -Stage.HIT_TARGET_POSITION - stage_padding_bottom);
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
                                // new ArgonKeyCounterDisplay(),
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
                    // return new DefaultSkinComponentsContainer(container =>
                    // {
                    // });

                case ManiaSkinComponentLookup maniaComponent:
                    switch (maniaComponent.Component)
                    {
                        case ManiaSkinComponents.StageBackground:
                            return new Ez2StageBackground();

                        case ManiaSkinComponents.ColumnBackground:
                            // if (Skin is Ez2Skin && resultComponent.Component >= HitResult.Perfect)
                            //     return Drawable.Empty();

                            return new Ez2ColumnBackground();

                        case ManiaSkinComponents.HoldNoteBody:
                            return new Ez2HoldBodyPiece();

                        case ManiaSkinComponents.HoldNoteTail:
                            return new Ez2HoldNoteTailPiece();

                        case ManiaSkinComponents.HoldNoteHead:
                            return new Ez2HoldNoteHeadPiece();

                        case ManiaSkinComponents.Note:
                            return new Ez2NotePiece();

                        case ManiaSkinComponents.HitTarget:
                            return new Ez2HitTarget();

                        case ManiaSkinComponents.KeyArea:
                            return new Ez2KeyArea();

                        case ManiaSkinComponents.HitExplosion:
                            return new Ez2HitExplosion();
                    }

                    break;
            }

            return base.GetDrawableComponent(lookup);
        }

        private static readonly Color4 colour_special = new Color4(206, 6, 3, 255);

        private static readonly Color4 colour_green = new Color4(100, 192, 92, 255);
        private static readonly Color4 colour_red = new Color4(206, 6, 3, 255);

        private static readonly Color4 colour_withe = new Color4(222, 222, 222, 255);
        private static readonly Color4 colour_blue = new Color4(55, 155, 255, 255);

        private const int total_colours = 3;

        private static readonly Color4 colour_cyan = new Color4(72, 198, 255, 255);
        private static readonly Color4 colour_pink = new Color4(213, 35, 90, 255);
        private static readonly Color4 colour_purple = new Color4(203, 60, 236, 255);

        private const int stage_padding_bottom = 25;

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                int columnIndex = maniaLookup.ColumnIndex ?? 0;
                var stage = beatmap.GetStageForColumnIndex(columnIndex);
                bool isSpecialColumn = stage.IsSpecialColumn(columnIndex);

                float columnWidth = (float)config.Get<double>(OsuSetting.ColumnWidth);
                float specialFactor = (float)config.Get<double>(OsuSetting.SpecialFactor);
                // float columnWidth = (float)config.Get<double>(ManiaRulesetSetting.ColumnWidth);
                // float specialFactor = (float)config.Get<double>(ManiaRulesetSetting.SpecialFactor);
                float width = columnWidth * (isSpecialColumn ? specialFactor : 1);

                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.BarLineHeight:
                        return SkinUtils.As<TValue>(new Bindable<float>(1));

                    case LegacyManiaSkinConfigurationLookups.ColumnSpacing:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingBottom:
                        return SkinUtils.As<TValue>(new Bindable<float>(stage_padding_bottom));

                    case LegacyManiaSkinConfigurationLookups.StagePaddingTop:
                        return SkinUtils.As<TValue>(new Bindable<float>(0));

                    case LegacyManiaSkinConfigurationLookups.ColumnWidth:

                        return SkinUtils.As<TValue>(new Bindable<float>(width));

                    case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:

                        var colour = getColourForLayout(columnIndex, stage);

                        return SkinUtils.As<TValue>(new Bindable<Color4>(colour));
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }

        private Color4 getColourForLayout(int columnIndex, StageDefinition stage)
        {
            columnIndex %= stage.Columns;

            switch (stage.Columns)
            {
                case 1:
                case 2:
                case 3:
                    return colour_special;

                case 4:
                    switch (columnIndex)
                    {
                        case 0: return colour_green;

                        case 1: return colour_red;

                        case 2: return colour_blue;

                        case 3: return colour_cyan;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 5:
                    switch (columnIndex)
                    {
                        case 0: return colour_green;

                        case 1: return colour_blue;

                        case 2: return colour_red;

                        case 3: return colour_cyan;

                        case 4: return colour_purple;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 7:
                    switch (columnIndex)
                    {
                        case 1:
                        case 5:
                            return colour_withe;

                        case 0:
                        case 2:
                        case 4:
                        case 6:
                            return colour_blue;

                        case 3:
                            return colour_green;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 8:
                    switch (columnIndex)
                    {
                        case 0:
                        case 4:
                            return colour_red;

                        case 2:
                        case 6:
                            return colour_withe;

                        case 1:
                        case 3:
                        case 5:
                        case 7:
                            return colour_blue;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 9:
                    switch (columnIndex)
                    {
                        case 0:
                        case 6:
                        case 7:
                            return colour_red;

                        case 2:
                        case 4:
                            return colour_withe;

                        case 1:
                        case 3:
                        case 5:
                            return colour_blue;

                        case 8:
                            return colour_green;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 10:
                    switch (columnIndex)
                    {
                        case 0:
                        case 9:
                            return colour_green;

                        case 2:
                        case 4:
                        case 5:
                        case 7:
                            return colour_withe;

                        case 1:
                        case 3:
                        case 6:
                        case 8:
                            return colour_blue;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 12:
                    switch (columnIndex)
                    {
                        case 0:
                        case 11:
                            return colour_red;

                        case 1:
                        case 3:
                        case 5:
                        case 6:
                        case 8:
                        case 10:
                            return colour_withe;

                        case 2:
                        case 4:
                        case 7:
                        case 9:
                            return colour_blue;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 14:
                    switch (columnIndex)
                    {
                        case 0:
                        case 12:
                        case 13:
                            return colour_red;

                        case 1:
                        case 3:
                        case 5:
                        case 7:
                        case 9:
                        case 11:
                            return colour_withe;

                        case 2:
                        case 4:
                        case 8:
                        case 10:
                            return colour_blue;

                        case 6:
                            return colour_green;

                        default: throw new ArgumentOutOfRangeException();
                    }

                case 16:
                    switch (columnIndex)
                    {
                        case 0:
                        case 6:
                        case 7:
                        case 8:
                        case 9:
                        case 15:
                            return colour_red;

                        case 1:
                        case 3:
                        case 5:
                        case 10:
                        case 12:
                        case 14:
                            return colour_withe;

                        case 2:
                        case 4:
                        case 11:
                        case 13:
                            return colour_blue;

                        default: throw new ArgumentOutOfRangeException();
                    }
            }

            // fallback for unhandled scenarios
            if (stage.IsSpecialColumn(columnIndex))
                return colour_special;

            switch (columnIndex % total_colours)
            {
                case 0: return colour_cyan;

                case 1: return colour_pink;

                case 2: return colour_purple;

                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
