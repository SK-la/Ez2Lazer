// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.BeatmapPools
{
    /// <summary>
    /// Optional ruleset-owned bridge between the unified path editor and a ruleset's scanner/importer.
    /// Providers are loaded as drawables so they may resolve game services through dependency injection.
    /// </summary>
    public abstract partial class RulesetBeatmapPoolProvider : Component
    {
        protected RulesetBeatmapPoolProvider(Ruleset ruleset)
        {
            Ruleset = ruleset;
        }

        public Ruleset Ruleset { get; }

        /// <summary>
        /// Whether this provider wants separate path profiles for gameplay variants.
        /// The default keeps one library per ruleset, which is appropriate for the four built-in modes.
        /// </summary>
        public virtual bool SeparatePathsByVariant => false;

        /// <summary>
        /// Whether applying paths performs scanning/importing. False means the manager is only storing paths.
        /// </summary>
        public virtual bool CanApply => false;

        public virtual LocalisableString ApplyButtonText => "Apply paths";

        public virtual IEnumerable<int> GetPathVariants() => SeparatePathsByVariant ? Ruleset.GameplayVariants : new[] { 0 };

        public virtual LocalisableString GetVariantName(int variant)
            => variant == 0 || !SeparatePathsByVariant ? "Default" : Ruleset.GetVariantName(variant);

        /// <summary>
        /// Returns legacy paths to import when no unified profile exists yet.
        /// </summary>
        public virtual IReadOnlyList<string> GetLegacyPaths(int variant) => Array.Empty<string>();

        /// <summary>
        /// Builds ruleset-specific controls displayed in the right-hand quarter of the manager.
        /// </summary>
        public virtual Drawable? CreateOptions() => null;

        /// <summary>
        /// Applies enabled existing directories for the selected profile.
        /// Disabled paths remain persisted but are intentionally absent from this list.
        /// </summary>
        public virtual Task ApplyAsync(int variant, IReadOnlyList<string> enabledPaths, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Default provider used for rulesets which have not opted into scanning yet.
    /// </summary>
    public sealed partial class StorageOnlyBeatmapPoolProvider : RulesetBeatmapPoolProvider
    {
        public StorageOnlyBeatmapPoolProvider(Ruleset ruleset)
            : base(ruleset)
        {
        }
    }
}
