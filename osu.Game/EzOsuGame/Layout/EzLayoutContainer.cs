// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// A skinnable-style container that reloads from <see cref="EzConfigLayoutStore"/> instead of the current skin.
    /// Lives on <see cref="EzLayoutLayer"/> so the official skin editor cannot see it.
    /// </summary>
    public partial class EzLayoutContainer : EzHudSkinnableContainer
    {
        private CompositeDrawable? dependencyDonor;

        public EzConfigLayoutStore Store { get; }

        public string LayoutFile { get; }

        public RulesetInfo? Ruleset { get; }

        public EzLayoutContainer(EzConfigLayoutStore store, string layoutFile, RulesetInfo? ruleset)
            : base(EzLayoutFiles.SkinLookup(layoutFile, ruleset)
                   ?? throw new ArgumentException($"Ez layout file {layoutFile} is not a skin area.", nameof(layoutFile)))
        {
            Store = store;
            LayoutFile = layoutFile;
            Ruleset = ruleset;
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Must be set before this drawable is loaded if child components need another screen's DI
        /// (for example <c>ScoreProcessor</c> from <c>Player</c>).
        /// </summary>
        public void SetDependencyDonor(CompositeDrawable? donor)
        {
            dependencyDonor = donor;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var baseDependencies = base.CreateChildDependencies(parent);

            if (dependencyDonor?.Dependencies == null)
                return baseDependencies;

            return new DependencyContainer(dependencyDonor.Dependencies);
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            // Never pull layout from the active skin.
            LoadFromStore();
        }

        public void LoadFromStore() => Reload(Store.CreateComponentsContainer(LayoutFile, Ruleset));

        internal bool Uses(EzConfigLayoutStore candidate) => ReferenceEquals(Store, candidate);

        /// <summary>
        /// Places a component into this container with sane defaults, mirroring the official skin editor's
        /// placement (top-centre anchor, vertically centred), then anchors it to the closest anchor/origin
        /// pair. Returns false if the container or the component cannot accept the addition (e.g. missing
        /// dependencies).
        /// </summary>
        /// <param name="component">The component to add.</param>
        /// <param name="applyDefaults">
        /// Whether to apply the default anchor / origin / position. Pasted and cloned components carry the
        /// serialised transform, so their placement must not overwrite it.
        /// </param>
        public bool TryPlace(ISerialisableDrawable component, bool applyDefaults = true)
        {
            if (component is not Drawable drawable)
                return false;

            if (applyDefaults)
            {
                drawable.Origin = Anchor.TopCentre;
                drawable.Anchor = Anchor.TopCentre;
                drawable.Y = DrawSize.Y / 2;
            }

            try
            {
                Add(component);
            }
            catch
            {
                return false;
            }

            if (applyDefaults && !component.UsesFixedAnchor)
                SkinSelectionHandler.ApplyClosestAnchorOrigin(drawable);

            return true;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            Alpha > 0 && InternalChildren.Any(c => c.IsPresent && c.ReceivePositionalInputAt(screenSpacePos));
    }

    /// <summary>
    /// A full-screen skin layer that can counteract the global interface scale while preserving its anchors.
    /// </summary>
    public partial class EzHudSkinnableContainer : SkinnableContainer
    {
        private IBindable<bool>? scaleSkinElements;
        private Container? compensation;

        public EzHudSkinnableContainer(GlobalSkinnableContainerLookup lookup)
            : base(lookup)
        {
            RelativeSizeAxes = Axes.Both;
            OnComponentsLoaded += _ => wrapComponents();
        }

        private void wrapComponents()
        {
            if (InternalChildren.FirstOrDefault() is not Container content)
                return;

            RemoveInternal(content, false);
            AddInternal(compensation = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor,
                Origin = Origin,
                Child = content,
            });
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            scaleSkinElements = ezConfig.GetBindable<bool>(Ez2Setting.ScaleSkinElements);
        }

        protected override void Update()
        {
            base.Update();

            if (compensation?.Parent != this)
                return;

            if (compensation.Anchor != Anchor)
                compensation.Anchor = Anchor;

            if (compensation.Origin != Origin)
                compensation.Origin = Origin;

            if (scaleSkinElements == null || Lookup.Lookup == GlobalSkinnableContainers.Playfield)
                return;

            float uiScale = 1;

            for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is not ScalingContainer.ScalingDrawSizePreservingFillContainer scalingContainer || !scalingContainer.AppliesUIScale)
                    continue;

                uiScale = scalingContainer.CurrentScale;
                break;
            }

            Vector2 containerSize = scaleSkinElements.Value ? Vector2.One : new Vector2(uiScale);
            Vector2 containerScale = scaleSkinElements.Value ? Vector2.One : new Vector2(1 / uiScale);

            if (compensation.Size != containerSize)
                compensation.Size = containerSize;

            if (compensation.Scale != containerScale)
                compensation.Scale = containerScale;
        }
    }
}
