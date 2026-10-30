// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez-style replacement for the official <see cref="osu.Game.Overlays.SkinEditor.SkinComponentToolbox"/>.
    /// Lists the components that can be placed into the active editing layer; clicking one places it,
    /// persists the layout, and selects it. Global and ruleset-specific libraries are separate
    /// instances of this section.
    /// </summary>
    public partial class EzSkinEditorComponentLibrarySection : FillFlowContainer
    {
        private readonly bool rulesetScoped;

        [Resolved(canBeNull: true)]
        private EzSkinEditorHudLayer? hudLayer { get; set; }

        [Resolved(canBeNull: true)]
        private ISkinComponentToolboxFilter? typeFilter { get; set; }

        /// <summary>
        /// Library title shown at the top of the section.
        /// </summary>
        public LocalisableString Title { get; init; }

        /// <param name="rulesetScoped">
        /// When true the library lists ruleset-specific components for the ruleset the editor is editing;
        /// when false it lists global components only. The ruleset is resolved live from the HUD layer so
        /// it always matches the container the components are placed into.
        /// </param>
        public EzSkinEditorComponentLibrarySection(bool rulesetScoped)
        {
            this.rulesetScoped = rulesetScoped;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 8);
            Padding = new MarginPadding(8);
        }

        /// <summary>The ruleset whose components this library lists, or null for the global library.</summary>
        private RulesetInfo? resolvedRuleset => rulesetScoped ? hudLayer?.CurrentRuleset : null;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hudLayer == null)
            {
                showMessage(EzEditorStrings.LIBRARY_PLACEHOLDER);
                return;
            }

            rebuild();
            hudLayer.ActiveTargetChanged += rebuild;
        }

        private void rebuild()
        {
            Clear();

            Add(new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = Title,
                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
            });

            Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = Colour4.White.Opacity(0.1f),
                Margin = new MarginPadding { Vertical = 2 },
            });

            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
            };

            Add(list);

            if (hudLayer == null)
                return;

            // A ruleset-scoped layer only accepts ruleset components; the global layer only global ones.
            // With no resolved ruleset yet, a mode library would otherwise list the global set under a
            // mode heading, so keep it empty until a ruleset is known.
            RulesetInfo? target = resolvedRuleset;

            if (rulesetScoped && target == null)
            {
                list.Add(emptyLabel());
                return;
            }

            foreach (var type in SerialisedDrawableInfo.GetAllAvailableDrawables(target))
                attemptAddComponent(list, type);

            if (list.Count == 0)
                list.Add(emptyLabel());
        }

        private OsuSpriteText emptyLabel() => new OsuSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = EzEditorStrings.LIBRARY_EMPTY,
            Font = OsuFont.Default.With(size: 13),
        };

        private void attemptAddComponent(FillFlowContainer list, Type type)
        {
            if (typeFilter?.IsExcluded(type) == true)
                return;

            try
            {
                Drawable instance = (Drawable)Activator.CreateInstance(type)!;

                if (!((ISerialisableDrawable)instance).IsEditable)
                    return;

                list.Add(new ComponentPlaceButton(type, instance, hudLayer?.ActiveTarget)
                {
                    Action = () => place(type),
                });
            }
            catch (DependencyNotRegisteredException)
            {
                // The component needs dependencies this layer cannot provide; hide it from the library.
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Ez skin component {type} could not be listed in the library");
            }
        }

        private void place(Type type)
        {
            if (hudLayer?.TryPlaceComponent(type) != true)
                Logger.Log($"Could not place Ez skin component {type.Name}; the active layer rejected it.");
        }

        private void showMessage(LocalisableString text)
        {
            Child = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = text,
                Font = OsuFont.Default.With(size: 14),
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (!IsLoaded)
                return;

            if (hudLayer != null)
                hudLayer.ActiveTargetChanged -= rebuild;
        }

        /// <summary>
        /// Library entry: a live instance of the component (scaled to fit a thumbnail), its type name,
        /// and a click handler that places it into the active layer.
        /// </summary>
        private partial class ComponentPlaceButton : EzButton
        {
            private const float thumbnail_height = 72;

            private readonly Drawable component;

            private Container previewContainer = null!;

            public ComponentPlaceButton(Type type, Drawable component, CompositeDrawable? dependencySource)
            {
                this.component = component;

                RelativeSizeAxes = Axes.X;
                Height = thumbnail_height + 24;

                previewContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = thumbnail_height,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = true,
                    Padding = new MarginPadding(8),
                };

                component.Anchor = Anchor.Centre;
                component.Origin = Anchor.Centre;
                previewContainer.Add(new DependencyBorrowingContainer(dependencySource) { RelativeSizeAxes = Axes.Both, Child = component });

                Add(previewContainer);

                Add(new OsuSpriteText
                {
                    Text = type.Name,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = OsuFont.Default.With(size: 12),
                });
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // A component with a zero-size axis (not yet laid out, or scrolled out of view and
                // culled) would otherwise divide to infinity. Guard every axis, not just the vector.
                if (!float.IsFinite(previewContainer.DrawWidth) || !float.IsFinite(previewContainer.DrawHeight)
                    || !float.IsFinite(component.DrawWidth) || !float.IsFinite(component.DrawHeight)
                    || component.DrawWidth <= 0 || component.DrawHeight <= 0
                    || previewContainer.DrawWidth <= 0 || previewContainer.DrawHeight <= 0)
                {
                    return;
                }

                float bestScale = Math.Min(
                    previewContainer.DrawWidth / component.DrawWidth,
                    previewContainer.DrawHeight / component.DrawHeight);

                if (float.IsFinite(bestScale) && bestScale > 0)
                    component.Scale = new Vector2(bestScale);
            }
        }

        /// <summary>
        /// Hosts a component preview using another container's dependency container, so components
        /// needing screen-scoped dependencies (score processor, etc.) still instantiate for preview.
        /// </summary>
        private partial class DependencyBorrowingContainer : Container
        {
            private readonly CompositeDrawable? donor;

            public DependencyBorrowingContainer(CompositeDrawable? donor)
            {
                this.donor = donor;
            }

            protected override bool ShouldBeConsideredForInput(Drawable child) => false;

            public override bool PropagateNonPositionalInputSubTree => false;

            public override bool PropagatePositionalInputSubTree => false;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var baseDependencies = base.CreateChildDependencies(parent);

                if (donor?.Dependencies == null)
                    return baseDependencies;

                return new DependencyContainer(donor.Dependencies);
            }
        }
    }
}
