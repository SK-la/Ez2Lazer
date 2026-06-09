// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorSceneBar : CompositeDrawable
    {
        public const float HEIGHT = SkinEditorSceneLibrary.HEIGHT;

        public Bindable<EzSkinEditorSceneType> CurrentScene { get; } = new Bindable<EzSkinEditorSceneType>(EzSkinEditorSceneType.Appearance);

        private readonly Dictionary<EzSkinEditorSceneType, SceneTabButton> tabButtons = new Dictionary<EzSkinEditorSceneType, SceneTabButton>();

        public EzSkinEditorSceneBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10),
                    Padding = new MarginPadding(10),
                    Children = EzSkinEditorSceneRegistry.All.Select(strategy =>
                    {
                        var button = new SceneTabButton(strategy.SceneType, strategy.TabTitle.ToString(), () => CurrentScene.Value = strategy.SceneType);
                        tabButtons[strategy.SceneType] = button;
                        return button;
                    }).ToArray(),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            CurrentScene.BindValueChanged(e => updateActiveTab(e.NewValue), true);
        }

        private void updateActiveTab(EzSkinEditorSceneType scene)
        {
            foreach (var (sceneType, button) in tabButtons)
                button.Active = sceneType == scene;
        }

        private partial class SceneTabButton : OsuButton
        {
            public EzSkinEditorSceneType SceneType { get; }

            private bool active;

            public bool Active
            {
                get => active;
                set
                {
                    active = value;
                    if (IsLoaded)
                        updateColours();
                }
            }

            private OsuColour colours = null!;

            public SceneTabButton(EzSkinEditorSceneType sceneType, string text, Action action)
            {
                SceneType = sceneType;
                Text = text;
                Action = action;
                Width = 100;
                Height = SkinEditorSceneLibrary.BUTTON_HEIGHT;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, OverlayColourProvider? overlayColourProvider)
            {
                this.colours = colours;
                BackgroundColour = overlayColourProvider?.Background3 ?? colours.Blue3;
                Content.CornerRadius = 5;
                updateColours();
            }

            private void updateColours()
            {
                BackgroundColour = active ? colours.YellowDark : colours.Blue3;
            }
        }
    }
}
