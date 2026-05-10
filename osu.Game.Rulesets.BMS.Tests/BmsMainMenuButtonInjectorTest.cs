// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.BMS.UI.MenuEntry;
using osu.Game.Screens.Menu;
using osuTK;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Regression guards for the BMS main-menu injection pipeline. These tests assert that
    /// the private osu.Game members the injector reflects against still exist and have
    /// compatible types — if osu! upstream renames them, these tests will fail loudly
    /// before users hit a silently disabled BMS Game button.
    /// </summary>
    [TestFixture]
    public class BmsMainMenuButtonInjectorTest
    {
        private const BindingFlags instance_non_public = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TestInjectorIsDrawableComponent()
        {
            Type injectorType = typeof(BmsMainMenuButtonInjector);

            Assert.That(typeof(Drawable).IsAssignableFrom(injectorType), "BmsMainMenuButtonInjector must be a Drawable so it can enter the OsuGame tree via CreateIcon().");
        }

        [Test]
        public void TestRulesetIconHostsTheInjector()
        {
            var icon = new BmsRulesetIcon();

            // CompositeDrawable.InternalChildren is protected; reflect to read.
            var internalChildrenProp = typeof(osu.Framework.Graphics.Containers.CompositeDrawable)
                .GetProperty("InternalChildren", instance_non_public);

            Assert.That(internalChildrenProp, Is.Not.Null, "InternalChildren property missing on CompositeDrawable (osu.Framework upstream change?).");

            var children = (IReadOnlyList<Drawable>)internalChildrenProp!.GetValue(icon)!;

            Assert.That(children.Any(c => c is BmsMainMenuButtonInjector), "BmsRulesetIcon must contain a BmsMainMenuButtonInjector as InternalChild.");
        }

        [Test]
        public void TestRulesetIconAllowsSizeAssignment()
        {
            // Regression guard for: PanelBeatmapSet.SpreadDisplay (and other consumers of Ruleset.CreateIcon())
            // directly assign .Size on the drawable. AutoSizeAxes makes that throw — must remain unset.
            var icon = new BmsRulesetIcon();

            Assert.That(icon.AutoSizeAxes, Is.EqualTo(Axes.None),
                "BmsRulesetIcon must not use AutoSizeAxes; consumers assign .Size directly.");

            Assert.DoesNotThrow(() => icon.Size = new Vector2(14),
                "Setting Size on BmsRulesetIcon must not throw — PanelBeatmapSet.SpreadDisplay relies on it.");

            Assert.That(icon.Size, Is.EqualTo(new Vector2(14)));
        }

        [Test]
        public void TestMainMenuStillExposesButtonsField()
        {
            FieldInfo? buttonsField = walkHierarchy(typeof(MainMenu), "Buttons");

            Assert.That(buttonsField, Is.Not.Null, "MainMenu.Buttons field has been renamed/removed; injector reflection is broken.");
            Assert.That(typeof(ButtonSystem).IsAssignableFrom(buttonsField!.FieldType), "MainMenu.Buttons type changed; injector reflection is broken.");
        }

        [Test]
        public void TestButtonSystemStillExposesPlayListAndButtonArea()
        {
            FieldInfo? buttonsPlay = typeof(ButtonSystem).GetField("buttonsPlay", instance_non_public);
            FieldInfo? buttonArea = typeof(ButtonSystem).GetField("buttonArea", instance_non_public);

            Assert.Multiple(() =>
            {
                Assert.That(buttonsPlay, Is.Not.Null, "ButtonSystem.buttonsPlay missing — injector cannot insert the BMS button.");
                Assert.That(buttonArea, Is.Not.Null, "ButtonSystem.buttonArea missing — injector cannot show the new button.");

                Assert.That(buttonsPlay!.FieldType.IsGenericType, Is.True);
                Assert.That(buttonsPlay.FieldType.GetGenericTypeDefinition(), Is.EqualTo(typeof(List<>)));
                Assert.That(buttonsPlay.FieldType.GetGenericArguments()[0], Is.EqualTo(typeof(MainMenuButton)));

                Assert.That(buttonArea!.FieldType, Is.EqualTo(typeof(ButtonArea)));
            });
        }

        [Test]
        public void TestButtonAreaFlowIsAccessible()
        {
            FieldInfo? flow = typeof(ButtonArea).GetField("Flow", BindingFlags.Instance | BindingFlags.Public);

            Assert.That(flow, Is.Not.Null, "ButtonArea.Flow has been removed; injector cannot attach the new button to the visible row.");
        }

        [Test]
        public void TestOsuGamePerformFromScreenIsPublic()
        {
            MethodInfo? performFromScreen = typeof(OsuGame).GetMethod("PerformFromScreen", BindingFlags.Instance | BindingFlags.Public);

            Assert.That(performFromScreen, Is.Not.Null, "OsuGame.PerformFromScreen has been moved/renamed; injector click handler is broken.");
        }

        private static FieldInfo? walkHierarchy(Type? type, string name)
        {
            for (Type? t = type; t != null; t = t.BaseType)
            {
                FieldInfo? field = t.GetField(name, instance_non_public);
                if (field != null)
                    return field;
            }

            return null;
        }
    }
}
