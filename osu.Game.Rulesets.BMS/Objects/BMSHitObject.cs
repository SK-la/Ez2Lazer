// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.BMS.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Objects
{
    /// <summary>
    /// Base class for all BMS hit objects.
    /// </summary>
    public abstract class BMSHitObject : HitObject, IHasColumn
    {
        private HitObjectProperty<int> column;

        public Bindable<int> ColumnBindable => column.Bindable;

        /// <summary>
        /// The column (lane) this hit object is in.
        /// </summary>
        public virtual int Column
        {
            get => column.Value;
            set => column.Value = value;
        }

        /// <summary>
        /// Whether this is a scratch lane note.
        /// </summary>
        public bool IsScratch { get; set; }

        protected override HitWindows CreateHitWindows() => new BMSHitWindows();
    }
}
