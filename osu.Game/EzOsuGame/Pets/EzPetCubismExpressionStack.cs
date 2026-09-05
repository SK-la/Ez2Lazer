// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;

namespace osu.Game.EzOsuGame.Pets
{
    public sealed class EzPetExpressionParam
    {
        public string Id { get; set; } = string.Empty;

        public float Value { get; set; }

        /// <summary>
        /// When true, <see cref="Value"/> is the amplitude of a sine oscillation around 0.
        /// </summary>
        public bool Oscillate { get; set; }

        public float Frequency { get; set; } = 2f;
    }

    public sealed class EzPetExpressionRecipe
    {
        public string Id { get; set; } = string.Empty;

        public List<EzPetExpressionParam> Params { get; set; } = new List<EzPetExpressionParam>();

        /// <summary>
        /// Seconds to keep full strength before release. 0 = hold until replaced.
        /// </summary>
        public float HoldSeconds { get; set; }

        public float AttackSeconds { get; set; } = 0.12f;

        public float ReleaseSeconds { get; set; } = 0.35f;

        /// <summary>
        /// Extra drawable bounce (0–1 peak) applied by the pet layer when this expression is active.
        /// </summary>
        public float VisualBounce { get; set; }
    }

    /// <summary>
    /// Built-in Cubism parameter recipes; pack JSON may override by id.
    /// </summary>
    public static class EzPetCubismExpressionLibrary
    {
        public static IReadOnlyDictionary<string, EzPetExpressionRecipe> CreateDefaults()
        {
            var map = new Dictionary<string, EzPetExpressionRecipe>(StringComparer.OrdinalIgnoreCase)
            {
                ["smile"] = recipe("smile", hold: 0, bounce: 0,
                    p(CubismDefaultParameterId.ParamMouthForm, 1f),
                    p(CubismDefaultParameterId.ParamEyeLSmile, 1f),
                    p(CubismDefaultParameterId.ParamEyeRSmile, 1f),
                    p(CubismDefaultParameterId.ParamCheek, 0.6f),
                    p(CubismDefaultParameterId.ParamBrowLY, 0.3f),
                    p(CubismDefaultParameterId.ParamBrowRY, 0.3f)),
                ["pout"] = recipe("pout", hold: 0, bounce: 0,
                    p(CubismDefaultParameterId.ParamMouthForm, -1f),
                    p(CubismDefaultParameterId.ParamBrowLY, -0.4f),
                    p(CubismDefaultParameterId.ParamBrowRY, -0.4f)),
                ["lookDown"] = recipe("lookDown", hold: 0, bounce: 0,
                    p(CubismDefaultParameterId.ParamAngleY, -18f),
                    p(CubismDefaultParameterId.ParamEyeBallY, -0.6f),
                    p(CubismDefaultParameterId.ParamBodyAngleY, -6f)),
                ["nod"] = recipe("nod", hold: 1.1f, bounce: 0,
                    osc(CubismDefaultParameterId.ParamAngleY, 14f, 3.2f),
                    osc(CubismDefaultParameterId.ParamBodyAngleY, 4f, 3.2f)),
                ["shake"] = recipe("shake", hold: 1.1f, bounce: 0,
                    osc(CubismDefaultParameterId.ParamAngleZ, 16f, 4f),
                    osc(CubismDefaultParameterId.ParamBodyAngleZ, 5f, 4f)),
                ["wave"] = recipe("wave", hold: 0, bounce: 0,
                    osc(CubismDefaultParameterId.ParamArmRA, 0.85f, 2.6f),
                    p(CubismDefaultParameterId.ParamArmLA, 0.25f),
                    osc(CubismDefaultParameterId.ParamHandR, 0.5f, 2.6f)),
                ["jump"] = recipe("jump", hold: 0.55f, bounce: 1f,
                    osc(CubismDefaultParameterId.ParamBodyAngleY, 8f, 6f)),
                ["kick"] = recipe("kick", hold: 0.8f, bounce: 0.15f,
                    osc(CubismDefaultParameterId.ParamBodyAngleZ, 10f, 5f)),
                ["coverEyes"] = recipe("coverEyes", hold: 0, bounce: 0,
                    p(CubismDefaultParameterId.ParamArmLA, 1f),
                    p(CubismDefaultParameterId.ParamArmRA, 1f),
                    p(CubismDefaultParameterId.ParamEyeLOpen, 0.15f),
                    p(CubismDefaultParameterId.ParamEyeROpen, 0.15f),
                    p(CubismDefaultParameterId.ParamBrowLY, -0.5f),
                    p(CubismDefaultParameterId.ParamBrowRY, -0.5f)),
            };

            return map;
        }

        public static Dictionary<string, EzPetExpressionRecipe> Merge(
            IReadOnlyDictionary<string, EzPetExpressionRecipe> defaults,
            IReadOnlyDictionary<string, EzPetExpressionRecipe>? overrides)
        {
            var merged = new Dictionary<string, EzPetExpressionRecipe>(defaults, StringComparer.OrdinalIgnoreCase);

            if (overrides == null)
                return merged;

            foreach ((string key, var recipe) in overrides)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                recipe.Id = string.IsNullOrWhiteSpace(recipe.Id) ? key : recipe.Id;
                merged[key] = recipe;
            }

            return merged;
        }

        /// <summary>
        /// Default clip → simultaneous expression ids when pack omits <c>clipExpressions</c>.
        /// </summary>
        public static IReadOnlyList<string> DefaultExpressionsForClip(string clipOrState)
        {
            if (string.IsNullOrWhiteSpace(clipOrState))
                return Array.Empty<string>();

            if (string.Equals(clipOrState, "idle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clipOrState, "hover", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clipOrState, "gameplay", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clipOrState, "enter", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();

            return clipOrState.ToLowerInvariant() switch
            {
                "poke" or "clear" or "nod" => new[] { "nod" },
                "grabbed" or "fail" or "miss" or "shake" => new[] { "shake" },
                "ranka" or "smile" or "proud" => new[] { "smile" },
                "ranks" => new[] { "smile", "wave" },
                "rankss" => new[] { "smile", "wave", "jump" },
                "rankb" => new[] { "lookDown", "pout", "kick" },
                "rankc" or "rankd" or "rankf" => new[] { "pout", "coverEyes" },
                "wave" => new[] { "wave" },
                "jump" => new[] { "jump" },
                "pout" => new[] { "pout" },
                "lookdown" => new[] { "lookDown" },
                "kick" => new[] { "kick" },
                "covereyes" => new[] { "coverEyes" },
                _ => new[] { clipOrState },
            };
        }

        private static EzPetExpressionRecipe recipe(string id, float hold, float bounce, params EzPetExpressionParam[] ps)
            => new EzPetExpressionRecipe
            {
                Id = id,
                HoldSeconds = hold,
                VisualBounce = bounce,
                Params = new List<EzPetExpressionParam>(ps),
            };

        private static EzPetExpressionParam p(string id, float value) => new EzPetExpressionParam { Id = id, Value = value };

        private static EzPetExpressionParam osc(string id, float amplitude, float hz) => new EzPetExpressionParam
        {
            Id = id,
            Value = amplitude,
            Oscillate = true,
            Frequency = hz,
        };
    }

    /// <summary>
    /// Simultaneously active expression layers applied via AddParameterValue.
    /// </summary>
    public sealed class EzPetCubismExpressionStack
    {
        private readonly Dictionary<string, EzPetExpressionRecipe> recipes;
        private readonly List<ActiveLayer> layers = new List<ActiveLayer>();
        private float time;

        public EzPetCubismExpressionStack(IReadOnlyDictionary<string, EzPetExpressionRecipe> recipes)
        {
            this.recipes = new Dictionary<string, EzPetExpressionRecipe>(recipes, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Peak drawable bounce 0–1 from active jump-like expressions.
        /// </summary>
        public float VisualBounce { get; private set; }

        public void SetRecipes(IReadOnlyDictionary<string, EzPetExpressionRecipe> next)
        {
            recipes.Clear();
            foreach ((string key, var recipe) in next)
                recipes[key] = recipe;
        }

        public void Clear()
        {
            layers.Clear();
            VisualBounce = 0;
        }

        public void Activate(IEnumerable<string> expressionIds)
        {
            layers.Clear();
            time = 0;

            foreach (string id in expressionIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!recipes.TryGetValue(id, out var recipe))
                {
                    // Unknown id with no recipe: treat as no-op (may still have motion3).
                    continue;
                }

                layers.Add(new ActiveLayer(recipe));
            }
        }

        public void Update(float dt)
        {
            time += dt;
            VisualBounce = 0;

            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                layer.Age += dt;

                float weight = layer.ComputeWeight();

                if (weight <= 0 && layer.IsFinished)
                {
                    layers.RemoveAt(i);
                    continue;
                }

                if (layer.Recipe.VisualBounce > 0)
                {
                    float pulse = MathF.Sin(Math.Clamp(layer.Age / Math.Max(0.05f, layer.Recipe.HoldSeconds + layer.Recipe.AttackSeconds), 0f, 1f) * MathF.PI);
                    VisualBounce = Math.Max(VisualBounce, layer.Recipe.VisualBounce * weight * Math.Max(0, pulse));
                }
            }
        }

        public void Apply(CubismModel model)
        {
            foreach (var layer in layers)
            {
                float weight = layer.ComputeWeight();
                if (weight <= 0.001f)
                    continue;

                foreach (var param in layer.Recipe.Params)
                {
                    if (string.IsNullOrWhiteSpace(param.Id))
                        continue;

                    float value = param.Oscillate
                        ? MathF.Sin(time * param.Frequency * MathF.Tau) * param.Value
                        : param.Value;

                    trySetAdd(model, param.Id, value * weight);
                }
            }
        }

        private static void trySetAdd(CubismModel model, string id, float delta)
        {
            try
            {
                int index = model.GetParameterIndex(id);
                if (index < 0)
                    return;

                model.AddParameterValue(id, delta);
            }
            catch
            {
                // Parameter absent on this model.
            }
        }

        private sealed class ActiveLayer
        {
            public ActiveLayer(EzPetExpressionRecipe recipe)
            {
                Recipe = recipe;
            }

            public EzPetExpressionRecipe Recipe { get; }

            public float Age { get; set; }

            public bool IsFinished => Recipe.HoldSeconds > 0
                                      && Age >= Recipe.AttackSeconds + Recipe.HoldSeconds + Recipe.ReleaseSeconds;

            public float ComputeWeight()
            {
                float attack = Math.Max(0.001f, Recipe.AttackSeconds);
                float release = Math.Max(0.001f, Recipe.ReleaseSeconds);

                if (Age < attack)
                    return Age / attack;

                if (Recipe.HoldSeconds <= 0)
                    return 1f;

                float holdEnd = attack + Recipe.HoldSeconds;
                if (Age < holdEnd)
                    return 1f;

                float releaseT = (Age - holdEnd) / release;
                if (releaseT >= 1f)
                    return 0f;

                return 1f - releaseT;
            }
        }
    }
}
