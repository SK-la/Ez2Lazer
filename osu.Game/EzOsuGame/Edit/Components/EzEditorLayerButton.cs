// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.SkinEditor;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Scene-bar selector for the active editing layer. Uses a popover so the option list is not
    /// clipped by the masked scene bar (same approach as <see cref="EzSkinEditorSkinDropdown"/>).
    /// </summary>
    public partial class EzEditorLayerButton : SkinEditorSceneLibrary.SceneButton, IHasPopover
    {
        public new const float DEFAULT_WIDTH = 160;

        /// <summary>Layers offered for the current scene, in display order.</summary>
        public Func<IReadOnlyList<EzSkinEditorLayer>>? GetAvailableLayers { get; set; }

        /// <summary>Localised label for a layer, including the current ruleset name where relevant.</summary>
        public Func<EzSkinEditorLayer, LocalisableString>? GetDisplayName { get; set; }

        /// <summary>
        /// The layer this button reflects. Owned by the scene bar; this button only reads it and
        /// pushes changes back through <see cref="SetLayer"/>.
        /// </summary>
        public Bindable<EzSkinEditorLayer>? ActiveLayer { get; set; }

        public EzEditorLayerButton()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Width = DEFAULT_WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (ActiveLayer != null)
                ActiveLayer.BindValueChanged(_ => RefreshDisplayText(), true);
        }

        /// <summary>
        /// Recomputes the header text. Call when the display name source changes without the layer
        /// changing (e.g. the active ruleset was switched).
        /// </summary>
        public void RefreshDisplayText() => Text = GetDisplayName?.Invoke(ActiveLayer?.Value ?? EzSkinEditorLayer.HudOff) ?? string.Empty;

        /// <summary>
        /// Applies a layer chosen in the popover. The popover never holds the shared bindable: a
        /// short-lived popover is disposed (and its fields <c>UnbindAll</c>ed) on close, which would
        /// otherwise clear this button's own subscription to <see cref="ActiveLayer"/>.
        /// </summary>
        private void SetLayer(EzSkinEditorLayer layer)
        {
            if (ActiveLayer != null)
                ActiveLayer.Value = layer;
        }

        public Popover GetPopover() => new LayerListPopover(
            GetAvailableLayers?.Invoke() ?? Array.Empty<EzSkinEditorLayer>(),
            GetDisplayName ?? (_ => string.Empty),
            ActiveLayer?.Value ?? EzSkinEditorLayer.HudOff,
            SetLayer);

        public partial class LayerListPopover : OsuPopover
        {
            private const float body_width = 220;
            private const float body_padding = 10;
            private const float item_height = 32;
            private const float item_spacing = 4;

            private readonly IReadOnlyList<EzSkinEditorLayer> layers;
            private readonly Func<EzSkinEditorLayer, LocalisableString> getDisplayName;
            private readonly EzSkinEditorLayer current;
            private readonly Action<EzSkinEditorLayer> onSelected;

            public LayerListPopover(IReadOnlyList<EzSkinEditorLayer> layers, Func<EzSkinEditorLayer, LocalisableString> getDisplayName, EzSkinEditorLayer current, Action<EzSkinEditorLayer> onSelected)
                : base(withPadding: false)
            {
                this.layers = layers;
                this.getDisplayName = getDisplayName;
                this.current = current;
                this.onSelected = onSelected;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Body auto-sizes; give it an explicit height derived from the item count. A relative
                // Content alone contributes nothing to an auto-sized Body, collapsing the popover.
                Width = body_width;
                Height = body_padding * 2 + layers.Count * item_height + Math.Max(0, layers.Count - 1) * item_spacing;

                Content.AutoSizeAxes = Axes.None;
                Content.RelativeSizeAxes = Axes.Both;

                AllowableAnchors = new[]
                {
                    Anchor.BottomLeft,
                    Anchor.BottomCentre,
                    Anchor.TopLeft,
                    Anchor.TopCentre,
                };

                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, item_spacing),
                    Padding = new MarginPadding(body_padding),
                };

                foreach (var layer in layers)
                {
                    flow.Add(new LayerSelectButton(getDisplayName(layer), () => select(layer))
                    {
                        Active = layer == current,
                    });
                }

                Child = flow;
            }

            private void select(EzSkinEditorLayer layer)
            {
                onSelected(layer);
                this.HidePopover();
            }

            public partial class LayerSelectButton : EzButton
            {
                public bool Active
                {
                    get => Accented;
                    set => Accented = value;
                }

                public LayerSelectButton(LocalisableString text, Action action)
                {
                    Text = text;
                    Action = action;
                    RelativeSizeAxes = Axes.X;
                    Height = 32;
                }
            }
        }
    }
}
