// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.EzOsuGame.Scoring
{
    public static class EzManiaScoreModeExtensions
    {
        public const int UNSET_MODE = -1;

        public static bool HasManiaGameplayModes(this ScoreInfo score)
        {
            if (score.Ruleset.OnlineID != 3)
                return false;

            return score.ManiaHitMode >= 0
                   && score.ManiaHealthMode >= 0;
        }

        public static void HydrateManiaModesFromSession(this ScoreInfo score)
        {
            if (score.Ruleset.OnlineID != 3)
                return;

            if (score.ManiaHitMode >= 0 && score.ManiaHealthMode >= 0)
                return;

            if (score.ID != Guid.Empty
                && EzManiaScoreModeSessionCache.TryGet(score.ID, out int hitMode, out int healthMode))
            {
                score.ManiaHitMode = hitMode;
                score.ManiaHealthMode = healthMode;
            }
        }

        public static void StoreManiaModesInSession(this ScoreInfo score)
        {
            if (score.Ruleset.OnlineID != 3)
                return;

            if (score.ManiaHitMode < 0 || score.ManiaHealthMode < 0)
                return;

            if (score.ID == Guid.Empty)
                return;

            EzManiaScoreModeSessionCache.Store(score.ID, score.ManiaHitMode, score.ManiaHealthMode);
        }

        public static bool TryGetManiaGameplayModes(this ScoreInfo score, out int hitMode, out int healthMode)
        {
            score.HydrateManiaModesFromSession();

            hitMode = score.ManiaHitMode;
            healthMode = score.ManiaHealthMode;

            if (score.Ruleset.OnlineID != 3)
                return false;

            if (hitMode >= 0 && healthMode >= 0)
                return true;

            hitMode = UNSET_MODE;
            healthMode = UNSET_MODE;
            return false;
        }

        public static void ApplyManiaGameplayModes(this ScoreInfo score, DrawableRuleset? drawableRuleset)
        {
            if (drawableRuleset?.ReplayScore != null)
                return;

            if (drawableRuleset is not IManiaGameplayModeSnapshot snapshot)
                return;

            score.ManiaHitMode = snapshot.HitMode;
            score.ManiaHealthMode = snapshot.HealthMode;
        }

        public static string GetHitModeDisplayName(int hitMode) => getEnumDescription<EzEnumHitMode>(hitMode);

        public static string GetHealthModeDisplayName(int healthMode) => getEnumDescription<EzEnumHealthMode>(healthMode);

        /// <summary>
        /// Creates a vertical mode label block for use outside <see cref="FillDirection.Full"/> containers.
        /// </summary>
        public static Drawable CreateDisplayDrawable(ScoreInfo score, float fontSize = 11, Anchor anchor = Anchor.TopLeft)
        {
            if (!score.TryGetManiaGameplayModes(out int hitMode, out int healthMode))
            {
                return new Container
                {
                    Anchor = anchor,
                    Origin = anchor,
                    AutoSizeAxes = Axes.Both,
                    Alpha = 0f,
                };
            }

            return new FillFlowContainer
            {
                Anchor = anchor,
                Origin = anchor,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new[]
                {
                    createModeLine("Hit", GetHitModeDisplayName(hitMode), fontSize, anchor),
                    createModeLine("HP", GetHealthModeDisplayName(healthMode), fontSize, anchor),
                }
            };
        }

        private static Drawable createModeLine(string label, string value, float fontSize, Anchor anchor) => new FillFlowContainer
        {
            Anchor = anchor,
            Origin = anchor,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Anchor = anchor,
                    Origin = anchor,
                    Font = OsuFont.GetFont(size: fontSize, weight: FontWeight.Bold),
                    Colour = OsuColour.Gray(0.65f),
                },
                new OsuSpriteText
                {
                    Text = value,
                    Anchor = anchor,
                    Origin = anchor,
                    Font = OsuFont.GetFont(size: fontSize, weight: FontWeight.SemiBold),
                },
            }
        };

        private static string getEnumDescription<T>(int value) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
                return value.ToString();

            var enumValue = (T)Enum.ToObject(typeof(T), value);
            var field = typeof(T).GetField(enumValue.ToString());

            return field?.GetCustomAttribute<DescriptionAttribute>()?.Description
                   ?? enumValue.ToString();
        }
    }
}
