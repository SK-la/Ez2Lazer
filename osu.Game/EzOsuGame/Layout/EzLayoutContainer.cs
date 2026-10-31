// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// A skinnable-style container that reloads from <see cref="EzLayoutStore"/> instead of the current skin.
    /// Lives on <see cref="EzLayoutLayer"/> so the official skin editor cannot see it.
    /// </summary>
    public partial class EzLayoutContainer : SkinnableContainer
    {
        private readonly EzLayoutStore store;
        private CompositeDrawable? dependencyDonor;

        public EzLayoutContainer(EzLayoutStore store, GlobalSkinnableContainerLookup lookup)
            : base(lookup)
        {
            this.store = store;
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

        public void LoadFromStore() => Reload(store.CreateComponentsContainer(Lookup));

        /// <summary>
        /// Places a freshly created component into this container with sane defaults, mirroring the
        /// official skin editor's placement (top-centre anchor, vertically centred), then anchors it
        /// to the closest anchor/origin pair. Returns false if the container or the component cannot
        /// accept the addition (e.g. missing dependencies).
        /// </summary>
        public bool TryPlace(ISerialisableDrawable component)
        {
            if (component is not Drawable drawable)
                return false;

            drawable.Origin = Anchor.TopCentre;
            drawable.Anchor = Anchor.TopCentre;
            drawable.Y = DrawSize.Y / 2;

            try
            {
                Add(component);
            }
            catch
            {
                return false;
            }

            if (!component.UsesFixedAnchor)
                SkinSelectionHandler.ApplyClosestAnchorOrigin(drawable);

            return true;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            Alpha > 0 && InternalChildren.Any(c => c.IsPresent && c.ReceivePositionalInputAt(screenSpacePos));
    }
}
