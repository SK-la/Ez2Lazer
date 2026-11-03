// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneEzHUDDynamicSpeedDisplay : OsuTestScene
    {
        private readonly BindableDouble speed = new BindableDouble(1)
        {
            MinValue = 0.5,
            MaxValue = 2.0,
            Precision = 0.01,
        };

        private readonly BindableBool showText = new BindableBool(true);
        private readonly BindableBool modShowLine = new BindableBool(true);

        private EzHUDDynamicSpeedDisplay display = null!;
        private ModNiceBPM? mod;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddToggleStep("Show speed text", value =>
            {
                if (display.IsNotNull())
                    showText.Value = value;
            });

            AddToggleStep("HUD show speed line", value =>
            {
                if (display.IsNotNull())
                    display.ShowSpeedLine.Value = value;
            });

            AddToggleStep("Mod show speed line", value =>
            {
                if (display.IsNotNull())
                {
                    if (mod != null)
                        mod.ShowSpeedLine.Value = value;
                    else
                        modShowLine.Value = value;
                }
            });

            AddSliderStep("Line width", 50, 600, 200, value =>
            {
                if (display.IsNotNull())
                    display.LineWidth.Value = value;
            });

            AddToggleStep("Endpoint blink", value =>
            {
                if (display.IsNotNull())
                    display.ShowEndpointBlink.Value = value;
            });

            AddStep("Set speed 0.80x", () => speed.Value = 0.8);
            AddStep("Set speed 1.00x", () => speed.Value = 1.0);
            AddStep("Set speed 1.20x", () => speed.Value = 1.2);
            AddStep("Set speed 1.50x", () => speed.Value = 1.5);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("Reset display", createStandaloneDisplay);
        }

        private void mountDisplay()
        {
            display.Anchor = Anchor.Centre;
            display.Origin = Anchor.Centre;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Gray,
                },
                display,
            };
        }

        private void createStandaloneDisplay()
        {
            mod = null;
            speed.Value = 1;
            display = new EzHUDDynamicSpeedDisplay();
            mountDisplay();
        }

        private void createModLinkedDisplay(bool modLineEnabled)
        {
            mod = null;
            speed.Value = 1;
            showText.Value = true;
            modShowLine.Value = modLineEnabled;

            display = new EzHUDDynamicSpeedDisplay(speed, showText, modShowLine);
            mountDisplay();
        }

        private void createModInjectedDisplay(bool modLineEnabled)
        {
            mod = new ModNiceBPM();

            if (!modLineEnabled)
                mod.ShowSpeedLine.Value = false;

            display = new EzHUDDynamicSpeedDisplay(mod.SpeedChange, mod.ShowSpeedText, mod.ShowSpeedLine);
            mountDisplay();
        }

        private float getSpeedLineWidth() => display.ChildrenOfType<Path>().Single().Size.X;

        [Test]
        public void TestStandaloneHudDefaultLineOn()
        {
            AddStep("Create standalone HUD", createStandaloneDisplay);
            AddAssert("Speed line visible by default", () => display.ShowSpeedLine.Value);
            AddUntilStep("Line path created", () => display.ChildrenOfType<Path>().Any());
            AddUntilStep("Line has draw width", () => getSpeedLineWidth() > 0);
        }

        [Test]
        public void TestModInjectedHudDrawsLineByDefault()
        {
            AddStep("Create mod-injected HUD", () => createModInjectedDisplay(modLineEnabled: true));
            AddAssert("Mod show speed line defaults on", () => mod!.ShowSpeedLine.Value);
            AddAssert("HUD synced to mod", () => display.ShowSpeedLine.Value);
            AddUntilStep("Line path created", () => display.ChildrenOfType<Path>().Any());
            AddUntilStep("Line has draw width", () => getSpeedLineWidth() > 0);
        }

        [Test]
        public void TestModInjectedHudHidesLineWhenModLineOff()
        {
            AddStep("Create mod-injected HUD with line off", () => createModInjectedDisplay(modLineEnabled: false));
            AddAssert("Mod show speed line off", () => !mod!.ShowSpeedLine.Value);
            AddAssert("HUD synced to mod", () => !display.ShowSpeedLine.Value);
            AddUntilStep("Line path still created", () => display.ChildrenOfType<Path>().Any());
            AddUntilStep("Line draw width zero", () => getSpeedLineWidth() == 0);
        }

        [Test]
        public void TestModLinkedLineSync()
        {
            AddStep("Create mod-linked HUD", () => createModLinkedDisplay(modLineEnabled: true));
            AddUntilStep("Line path created", () => display.ChildrenOfType<Path>().Any());
            AddAssert("Mod line on", () => modShowLine.Value);
            AddAssert("HUD line synced on", () => display.ShowSpeedLine.Value);
            AddUntilStep("Line has draw width", () => getSpeedLineWidth() > 0);

            AddStep("Disable via HUD", () => display.ShowSpeedLine.Value = false);
            AddAssert("Mod line synced off", () => !modShowLine.Value);
            AddUntilStep("Line draw width zero", () => getSpeedLineWidth() == 0);

            AddStep("Enable via mod", () => modShowLine.Value = true);
            AddAssert("HUD line synced on", () => display.ShowSpeedLine.Value);
            AddUntilStep("Line has draw width again", () => getSpeedLineWidth() > 0);
        }

        [Test]
        public void TestDynamicSpeedOscillation()
        {
            AddStep("Create HUD with external speed source", () =>
            {
                mod = null;
                speed.Value = 1;
                display = new EzHUDDynamicSpeedDisplay(speed, showText);
                mountDisplay();
            });

            AddRepeatStep("Oscillate speed", () => speed.Value = speed.Value > 1 ? 0.85 : 1.35, 20);
        }
    }
}
