// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneScalingContainer : OsuTestScene
    {
        private OsuConfigManager osuConfigManager { get; set; }

        private Ez2ConfigManager ezConfigManager { get; set; }

        private EzHudSkinnableContainer skinScaleContainer;
        private ScaleTestComponent testComponent;

        private ScalingContainer scaling1;
        private ScalingContainer scaling2;
        private Box scaleTarget;

        [BackgroundDependencyLoader]
        private void load()
        {
            osuConfigManager = new OsuConfigManager(LocalStorage);
            ezConfigManager = new Ez2ConfigManager(LocalStorage);

            Dependencies.CacheAs(osuConfigManager);
            Dependencies.CacheAs(ezConfigManager);

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        scaling1 = new ScalingContainer(ScalingMode.Everything)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(0.8f),
                            Children = new Drawable[]
                            {
                                scaling2 = new ScalingContainer(ScalingMode.Everything)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Scale = new Vector2(0.8f),
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            Colour = Color4.Purple,
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        scaleTarget = new Box
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Colour = Color4.White,
                                            Size = new Vector2(100),
                                        },
                                        skinScaleContainer = new EzHudSkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))
                                        {
                                            Anchor = Anchor.BottomRight,
                                            Origin = Anchor.BottomRight,
                                        },
                                    }
                                }
                            }
                        }
                    }
                },
            };
        }

        [Test]
        public void TestScaling()
        {
            AddStep("adjust scale", () => osuConfigManager.SetValue(OsuSetting.UIScale, 2f));

            checkForCorrectness();

            AddStep("adjust scale", () => osuConfigManager.SetValue(OsuSetting.UIScale, 0.5f));

            checkForCorrectness();
        }

        [Test]
        public void TestSkinElementScaleCanBeIndependentOfUIScale()
        {
            Quad baseline = default;

            AddStep("set scale defaults", () =>
            {
                osuConfigManager.SetValue(OsuSetting.UIScale, 1f);
                ezConfigManager.SetValue(Ez2Setting.ScaleSkinElements, false);
            });

            AddUntilStep("skin scale container loaded", () => skinScaleContainer.ComponentsLoaded);
            AddStep("add test component", () => skinScaleContainer.Add(testComponent = new ScaleTestComponent()));
            AddUntilStep("test component loaded", () => testComponent.IsLoaded);
            AddStep("capture baseline", () => baseline = testComponent.ScreenSpaceDrawQuad);

            AddStep("change interface scale while independent", () => osuConfigManager.SetValue(OsuSetting.UIScale, 1.4f));
            AddWaitStep("finish interface scale animation", 40);
            AddAssert("skin component retains size and position", () => testComponent.ScreenSpaceDrawQuad.AlmostEquals(baseline));

            AddStep("enable skin element scaling", () => ezConfigManager.SetValue(Ez2Setting.ScaleSkinElements, true));
            AddWaitStep("apply skin element scaling", 2);
            AddAssert("skin component follows interface scale", () => Math.Abs(
                testComponent.ScreenSpaceDrawQuad.AABBFloat.Width - baseline.AABBFloat.Width * 1.4f) < 0.5f);

            AddStep("restore scale defaults", () =>
            {
                osuConfigManager.SetValue(OsuSetting.UIScale, 1f);
                ezConfigManager.SetValue(Ez2Setting.ScaleSkinElements, false);
            });
        }

        private void checkForCorrectness()
        {
            Quad? scaling1LastQuad = null;
            Quad? scaling2LastQuad = null;
            Quad? scalingTargetLastQuad = null;

            AddUntilStep("ensure dimensions don't change", () =>
            {
                if (scaling1LastQuad.HasValue && scaling2LastQuad.HasValue)
                {
                    // check inter-frame changes to make sure they match expectations.
                    Assert.That(scaling1.ScreenSpaceDrawQuad.AlmostEquals(scaling1LastQuad.Value), Is.True);
                    Assert.That(scaling2.ScreenSpaceDrawQuad.AlmostEquals(scaling2LastQuad.Value), Is.True);
                }

                scaling1LastQuad = scaling1.ScreenSpaceDrawQuad;
                scaling2LastQuad = scaling2.ScreenSpaceDrawQuad;

                // wait for scaling to stop.
                bool scalingFinished = scalingTargetLastQuad.HasValue && scaleTarget.ScreenSpaceDrawQuad.AlmostEquals(scalingTargetLastQuad.Value);

                scalingTargetLastQuad = scaleTarget.ScreenSpaceDrawQuad;

                return scalingFinished;
            });
        }

        private partial class ScaleTestComponent : CompositeDrawable, ISerialisableDrawable
        {
            public bool UsesFixedAnchor { get; set; }

            public ScaleTestComponent()
            {
                Anchor = Anchor.TopLeft;
                Origin = Anchor.TopLeft;
                Position = new Vector2(40, 50);
                Size = new Vector2(100);
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Red,
                };
            }
        }
    }
}
