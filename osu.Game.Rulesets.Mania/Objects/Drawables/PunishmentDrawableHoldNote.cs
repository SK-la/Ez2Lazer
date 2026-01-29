// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="PunishmentHoldNote"/> hit object with special punishment logic.
    /// </summary>
    public partial class PunishmentDrawableHoldNote : DrawableHoldNote
    {
        [Resolved]
        private HealthProcessor healthProcessor { get; set; }

        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; }

        /// <summary>
        /// Whether to use health cap reduction instead of direct health deduction on combo breaks.
        /// When enabled, combo breaks reduce the maximum health cap by 15% each time, down to a minimum of 40%.
        /// </summary>
        private bool useHealthCapReduction = false;

        /// <summary>
        /// Current health cap multiplier (1.0 = 100% cap, 0.4 = 40% cap minimum)
        /// </summary>
        private float currentHealthCap = 1.0f;

        public PunishmentDrawableHoldNote(HoldNote hitObject)
            : base(hitObject)
        {
        }

        /// <summary>
        /// Enables or disables health cap reduction mode.
        /// When enabled, combo breaks reduce the maximum health cap instead of direct health deduction.
        /// </summary>
        /// <param name="enabled">Whether to enable health cap reduction mode.</param>
        public void SetHealthCapReductionMode(bool enabled)
        {
            useHealthCapReduction = enabled;
            if (!enabled)
            {
                // Reset health cap when disabling
                currentHealthCap = 1.0f;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Listen for combo break events
            if (scoreProcessor != null)
            {
                scoreProcessor.Combo.ValueChanged += OnComboChanged;
            }
        }

        private void OnComboChanged(ValueChangedEvent<int> combo)
        {
            // If combo broke (went to 0 or decreased significantly)
            if (combo.NewValue < combo.OldValue && combo.NewValue == 0)
            {
                if (healthProcessor != null)
                {
                    if (useHealthCapReduction)
                    {
                        // Reduce health cap by 15%, minimum 40%
                        currentHealthCap = Math.Max(0.4f, currentHealthCap - 0.15f);

                        // If current health exceeds the new cap, reduce it to the cap
                        if (healthProcessor.Health.Value > currentHealthCap)
                        {
                            healthProcessor.Health.Value = currentHealthCap;
                        }
                    }
                    else
                    {
                        // Apply punishment: deduct 25% health
                        healthProcessor.Health.Value -= 0.25f;
                    }
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (scoreProcessor != null)
            {
                scoreProcessor.Combo.ValueChanged -= OnComboChanged;
            }
        }
    }
}
