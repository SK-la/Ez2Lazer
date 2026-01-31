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
        private HealthProcessor? healthProcessor { get; set; }

        private float currentHealthCap = 1.0f;

        private PunishmentHoldNote punishmentHitObject => (PunishmentHoldNote)HitObject;

        public PunishmentDrawableHoldNote(HoldNote hitObject)
            : base(hitObject)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsHolding.BindValueChanged(OnHoldingChanged);
        }

        private void OnHoldingChanged(ValueChangedEvent<bool> holding)
        {
            // 追踪长按松开，只在以下情况触发：
            // 1. 正在长按中松开 (!holding.NewValue)
            // 2. 当前时间在音符持续时间内（在开始之后但在结束之前）
            // 3. 音符尚未完全判定（以避免在正常结束时触发）
            if (!holding.NewValue && holding.OldValue &&
                Time.Current >= HitObject.StartTime && Time.Current < HitObject.GetEndTime() &&
                !AllJudged)
            {
                triggerPunishment();
            }
        }

        private void triggerPunishment()
        {
            if (healthProcessor != null)
            {
                if (punishmentHitObject.UseHealthCapReduction)
                {
                    // 每次扣上限，最低40%
                    currentHealthCap = Math.Max(0.4f, currentHealthCap - 0.15f);

                    // 降低上限
                    if (healthProcessor.Health.Value > currentHealthCap)
                    {
                        healthProcessor.Health.Value = currentHealthCap;
                    }
                }
                else
                {
                    // 只扣血
                    healthProcessor.Health.Value -= 0.25f;
                }
            }
        }
    }
}
