// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            Alpha > 0 && InternalChildren.Any(c => c.IsPresent && c.ReceivePositionalInputAt(screenSpacePos));
    }
}
