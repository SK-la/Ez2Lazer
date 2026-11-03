// ============================================================================
// ManiaTestScriptedSkinTransformer.csx
// ============================================================================
// 推荐命名：Mania<SkinName>SkinTransformer.csx
// 兼容旧命名：ManiaTransformer.csx
//
// 这个示例展示的是“脚本皮肤的 Mania 专用 transformer”。
// 它在主皮肤脚本的基础上，为 Mania 提供专用配置和组件。
// ============================================================================

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.Mania.EzMania.HUD;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

public class ManiaTestScriptedSkinTransformer : SkinTransformer
{
    private readonly ManiaBeatmap beatmap;

    public ManiaTestScriptedSkinTransformer(ISkin skin, IBeatmap beatmap)
        : base(skin)
    {
        this.beatmap = (ManiaBeatmap)beatmap;
    }

    public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
    {
        switch (lookup)
        {
            case ManiaSkinComponentLookup maniaComponent:
                switch (maniaComponent.Component)
                {
                    case ManiaSkinComponents.ColumnBackground:
                        return new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(0.12f, 0.16f, 0.22f, 1f),
                        };
                }
                break;

            case GlobalSkinnableContainerLookup containerLookup:
                if (containerLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents)
                {
                    return new DefaultSkinComponentsContainer(container =>
                    {
                        var hitTiming = container.ChildrenOfType<EzHUDHitTiming>().ToArray();
                        if (hitTiming.Length >= 2)
                        {
                            hitTiming[0].Anchor = Anchor.Centre;
                            hitTiming[0].Origin = Anchor.Centre;
                            hitTiming[0].X = -500;
                            hitTiming[1].Anchor = Anchor.Centre;
                            hitTiming[1].Origin = Anchor.Centre;
                            hitTiming[1].X = 500;
                        }
                    })
                    {
                        new EzHUDHitTiming(),
                        new EzHUDHitTiming(),
                        new EzHUDComboTitle(),
                        new EzHUDComboCounter(),
                        new EzHUDKeyCounterDisplay(),
                        new EzHUDHitTimingColumns(),
                        new BarHitErrorMeter(),
                        new EzHUDHitResultScore(),
                    };
                }
                break;
        }

        return base.GetDrawableComponent(lookup);
    }

    public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
    {
        if (lookup is ManiaSkinConfigurationLookup maniaLookup)
        {
            switch (maniaLookup.Lookup)
            {
                case LegacyManiaSkinConfigurationLookups.ColumnWidth:
                    return SkinUtils.As<TValue>(new Bindable<float>(64));

                case LegacyManiaSkinConfigurationLookups.HitPosition:
                    return SkinUtils.As<TValue>(new Bindable<float>(430));

                case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:
                    return SkinUtils.As<TValue>(new Bindable<Color4>(Color4.Black));
            }
        }

        return base.GetConfig<TLookup, TValue>(lookup);
    }
}
