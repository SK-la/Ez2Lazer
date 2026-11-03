// ============================================================================
// TestScriptedSkin.csx
// ============================================================================
// 推荐命名：<SkinName>Skin.csx
// 兼容旧命名：Skin.csx
//
// 这个示例展示的是“脚本皮肤主文件”的最小可用模板。
// 它是一个 Skin 子类：
// - 可以复用现有皮肤资源
// - 可以通过 CreateInfo() 暴露脚本元数据
// - 可以被脚本加载器自动识别
// ============================================================================

using System.Collections.Generic;
using JetBrains.Annotations;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Beatmaps.Formats;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

public class TestScriptedSkin : Skin
{
    public static SkinInfo CreateInfo() => new SkinInfo
    {
        Name = "TestScriptedSkin",
        Creator = "Your Name",
        Protected = true,
        InstantiationInfo = typeof(TestScriptedSkin).GetInvariantInstantiationInfo(),
    };

    protected readonly IStorageResourceProvider Resources;

    public TestScriptedSkin(IStorageResourceProvider resources)
        : this(CreateInfo(), resources)
    {
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    public TestScriptedSkin(SkinInfo skin, IStorageResourceProvider resources, IResourceStore<byte[]>? fallbackStore = null)
        : base(skin, resources, fallbackStore)
    {
        Resources = resources;
    }

    public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => Textures?.Get(componentName, wrapModeS, wrapModeT);

    public override ISample GetSample(ISampleInfo sampleInfo)
    {
        string lookup = sampleInfo.LookupNames.FirstOrDefault() ?? "virtual";
        return new SampleVirtual(lookup);
    }

    public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
    {
        switch (lookup)
        {
            case GlobalSkinnableContainerLookup containerLookup:
                switch (containerLookup.Lookup)
                {
                    case GlobalSkinnableContainers.MainHUDComponents:
                        return new DefaultSkinComponentsContainer(container =>
                        {
                            var score = container.OfType<EzHUDScoreCounter>().FirstOrDefault();
                            if (score != null)
                            {
                                score.Anchor = Anchor.TopLeft;
                                score.Origin = Anchor.TopLeft;
                                score.Position = new Vector2(20, 20);
                                score.ShowLabel.Value = false;
                            }
                        })
                        {
                            Children = new Drawable[]
                            {
                                new EzHUDScoreCounter(),
                                new DefaultHealthDisplay(),
                                new JudgementCounterDisplay
                                {
                                    FillMode = FillMode.Fill,
                                    FlowDirection = { Value = Direction.Vertical },
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Position = new Vector2(20, 0),
                                },
                            }
                        };
                }

                return null;
        }

        return base.GetDrawableComponent(lookup);
    }

    public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
    {
        switch (lookup)
        {
            case GlobalSkinColours global:
                if (global == GlobalSkinColours.ComboColours)
                    return SkinUtils.As<TValue>(new Bindable<IReadOnlyList<Color4>?>(Configuration.ComboColours));
                break;

            case SkinComboColourLookup comboColour:
                return SkinUtils.As<TValue>(new Bindable<Color4>(getComboColour(Configuration, comboColour.ColourIndex)));
        }

        return null;
    }

    private static Color4 getComboColour(IHasComboColours source, int colourIndex) => source.ComboColours![colourIndex % source.ComboColours.Count];
}
