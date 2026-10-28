// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 侧栏右侧垂直分组轨导航栏：底色与场景库一致，按钮为 Ez 渐变开关态。
    /// </summary>
    public partial class EzSkinEditorSidebarGroupRail : CompositeDrawable
    {
        public const float WIDTH = 56;

        public readonly BindableInt SelectedIndex = new BindableInt();

        private readonly List<(LocalisableString Title, bool Visible)> items = new List<(LocalisableString, bool)>();
        private FillFlowContainer buttonFlow = null!;

        public EzSkinEditorSidebarGroupRail()
        {
            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background6,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = buttonFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, EzUIStyleV1.SCENE_BAR_BUTTON_SPACING),
                        Padding = new MarginPadding(EzUIStyleV1.SCENE_BAR_GAP),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SelectedIndex.BindValueChanged(_ => updateActive(), true);
        }

        public void SetItems(IReadOnlyList<(LocalisableString Title, bool Visible)> next)
        {
            items.Clear();
            items.AddRange(next);
            rebuildButtons();
        }

        private void rebuildButtons()
        {
            buttonFlow.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var (title, visible) = items[i];
                int index = i;

                var button = new RailButton(title, () => SelectedIndex.Value = index)
                {
                    Alpha = visible ? 1 : 0,
                    AlwaysPresent = visible,
                };

                if (!visible)
                    button.Hide();

                buttonFlow.Add(button);
            }

            if (items.Count == 0)
                return;

            if (SelectedIndex.Value < 0 || SelectedIndex.Value >= items.Count || !items[SelectedIndex.Value].Visible)
            {
                int firstVisible = items.FindIndex(i => i.Visible);
                SelectedIndex.Value = firstVisible >= 0 ? firstVisible : 0;
            }

            updateActive();
        }

        private void updateActive()
        {
            int i = 0;

            foreach (var child in buttonFlow)
            {
                if (child is RailButton button)
                    button.Active = i == SelectedIndex.Value;

                i++;
            }
        }

        private partial class RailButton : EzButton
        {
            public bool Active
            {
                get => Accented;
                set => Accented = value;
            }

            public RailButton(LocalisableString title, Action action)
            {
                Text = title;
                Action = action;
                RelativeSizeAxes = Axes.X;
                Height = EzUIStyleV1.SceneBarControlHeight();
                TextSize = 12;
                TextMargin = new MarginPadding(4);
            }
        }
    }
}
