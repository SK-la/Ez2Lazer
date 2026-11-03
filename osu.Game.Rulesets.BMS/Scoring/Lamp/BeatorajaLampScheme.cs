// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Default lamp scheme: mirrors beatoraja's <c>ClearType</c> rules.
    /// <para>
    /// Resolution order (highest tier wins):
    /// </para>
    /// <list type="number">
    /// <item><description>NoPlay if there's no recorded play.</description></item>
    /// <item><description>Failed if gameplay didn't clear the chart on a real gauge.</description></item>
    /// <item><description>Max if the play used the strictest hit window and earned every PG.</description></item>
    /// <item><description>Perfect if every judgement was PG (top-tier) regardless of window.</description></item>
    /// <item><description>FullCombo if there were zero misses.</description></item>
    /// <item><description>Otherwise the lamp tier follows the gauge that cleared:
    /// LightAssist / Easy / Normal / Hard / ExHard, with assist gauges mapped to AssistEasy.</description></item>
    /// </list>
    /// <para>
    /// To swap in a custom scheme (LR2-style, sliding scale, network ranking), implement
    /// <see cref="IBmsLampScheme"/> directly or subclass this and override <see cref="ResolveLamp"/>.
    /// </para>
    /// </summary>
    public class BeatorajaLampScheme : IBmsLampScheme
    {
        public virtual string DisplayName => "Beatoraja";

        public virtual BmsClearLamp ResolveLamp(BmsLampContext context)
        {
            if (!context.HasPlayed)
                return BmsClearLamp.NoPlay;

            if (!context.Cleared)
                return BmsClearLamp.Failed;

            if (context.IsAllPerfect && context.UsedHighestJudgementWindow)
                return BmsClearLamp.Max;

            if (context.IsAllPerfect)
                return BmsClearLamp.Perfect;

            if (context.IsFullCombo)
                return BmsClearLamp.FullCombo;

            return context.Gauge switch
            {
                BmsGaugeType.Assist => BmsClearLamp.AssistEasy,
                BmsGaugeType.LightAssistEasy => BmsClearLamp.LightAssistEasy,
                BmsGaugeType.Easy => BmsClearLamp.Easy,
                BmsGaugeType.Normal or BmsGaugeType.PNormal => BmsClearLamp.Normal,
                BmsGaugeType.Hard or BmsGaugeType.PHard => BmsClearLamp.Hard,
                BmsGaugeType.ExHard or BmsGaugeType.PExHard => BmsClearLamp.ExHard,
                BmsGaugeType.FullCombo => BmsClearLamp.FullCombo,
                _ => BmsClearLamp.Normal,
            };
        }

        public virtual Color4 GetLampColour(BmsClearLamp lamp) => BmsLampPalette.GetDefaultColour(lamp);

        public virtual Color4 GetLampTextColour(BmsClearLamp lamp) => BmsLampPalette.GetDefaultTextColour(lamp);
    }
}
