// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    internal partial class SkinSettingsToolbox : EditorSidebarSection
    {
        private const float position_slider_range = 2000;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        protected override Container<Drawable> Content { get; }

        private readonly Drawable component;

        private readonly BindableFloat positionX = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private readonly BindableFloat positionY = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private bool internalPositionUpdate;

        public SkinSettingsToolbox(Drawable component)
            : base(SkinEditorStrings.Settings(component.GetType().Name))
        {
            this.component = component;

            base.Content.Add(Content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var controls = component.CreateSettingsControls().ToArray();
            Drawable[] positionControls = Array.Empty<Drawable>();

            if (isEzHudComponent(component))
            {
                positionX.Value = component.Position.X;
                positionY.Value = component.Position.Y;

                positionX.BindValueChanged(_ => updateComponentPositionFromSliders());
                positionY.BindValueChanged(_ => updateComponentPositionFromSliders());

                positionControls = createPositionControls();
                Content.AddRange(positionControls);
            }

            Content.AddRange(controls);

            // track any changes to update undo states.
            foreach (var c in controls.Concat(positionControls).OfType<ISettingsItem>())
            {
                // TODO: SettingChanged is called too often for cases like SettingsTextBox and SettingsSlider.
                // We will want to expose a SettingCommitted or similar to make this work better.
                c.SettingChanged += () => changeHandler?.SaveState();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!isEzHudComponent(component) || internalPositionUpdate)
                return;

            if (positionX.Value != component.Position.X)
                positionX.Value = component.Position.X;

            if (positionY.Value != component.Position.Y)
                positionY.Value = component.Position.Y;
        }

        private Drawable[] createPositionControls() => new Drawable[]
        {
            new SettingsSlider<float>
            {
                LabelText = "Position X",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionX,
                KeyboardStep = positionX.Precision,
            },
            new SettingsSlider<float>
            {
                LabelText = "Position Y",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionY,
                KeyboardStep = positionY.Precision,
            }
        };

        private void updateComponentPositionFromSliders()
        {
            internalPositionUpdate = true;
            component.Position = new Vector2(positionX.Value, positionY.Value);
            internalPositionUpdate = false;
        }

        private static bool isEzHudComponent(Drawable drawable)
        {
            string? fullNamespace = drawable.GetType().Namespace;
            return fullNamespace?.Contains(".EzOsuGame.HUD") == true || fullNamespace?.Contains(".EzMania.HUD") == true;
        }
    }
}
