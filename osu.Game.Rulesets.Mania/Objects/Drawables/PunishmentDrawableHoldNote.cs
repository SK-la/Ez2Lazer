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
        /// Current health cap multiplier (1.0 = 100% cap, 0.4 = 40% cap minimum)
        /// </summary>
        private float currentHealthCap = 1.0f;

        private PunishmentHoldNote PunishmentHitObject => (PunishmentHoldNote)HitObject;

        public PunishmentDrawableHoldNote(HoldNote hitObject)
            : base(hitObject)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Listen for holding state changes to detect when player releases the key
            IsHolding.BindValueChanged(OnHoldingChanged);
        }

        private void OnHoldingChanged(ValueChangedEvent<bool> holding)
        {
            // Trigger punishment every time player releases the key during the note's duration
            // Only trigger if:
            // 1. Player was holding and now released (!holding.NewValue)
            // 2. Current time is within the note's duration (after start but before end)
            // 3. The note hasn't been fully judged yet (to avoid triggering on normal release at end)
            if (!holding.NewValue && holding.OldValue &&
                Time.Current >= HitObject.StartTime && Time.Current < HitObject.GetEndTime() &&
                !AllJudged)
            {
                TriggerPunishment();
            }
        }

        private void TriggerPunishment()
        {
            if (healthProcessor != null)
            {
                if (PunishmentHitObject.UseHealthCapReduction)
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

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // No need to unsubscribe from combo changes anymore
        }
    }
}
