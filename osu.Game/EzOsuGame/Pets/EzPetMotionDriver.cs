// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osuTK;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Drives normalised pet-box motion (wander / moveTo / teleport) independent of clip frames.
    /// </summary>
    public class EzPetMotionDriver
    {
        private EzPetMotionDefinition? active;
        private Vector2 wanderTarget;
        private Vector2 moveFrom;
        private Vector2 moveTo;
        private double moveElapsedMs;
        private double moveDurationMs = 400;
        private Easing moveEasing = Easing.OutQuad;
        private readonly Random random = new Random();

        public bool IsActive => active != null;

        public void Stop()
        {
            active = null;
        }

        public void Start(EzPetMotionDefinition? motion, Vector2 currentNormalised, Func<string, Vector2?>? resolveAnchor)
        {
            if (motion == null || string.IsNullOrWhiteSpace(motion.Mode))
            {
                Stop();
                return;
            }

            active = motion;
            string mode = motion.Mode.Trim();

            if (string.Equals(mode, "teleportTo", StringComparison.OrdinalIgnoreCase))
            {
                var target = resolveTarget(motion, currentNormalised, resolveAnchor);
                wanderTarget = target;
                moveTo = target;
                moveFrom = target;
                moveElapsedMs = moveDurationMs = 0;
                return;
            }

            if (string.Equals(mode, "moveTo", StringComparison.OrdinalIgnoreCase))
            {
                moveFrom = currentNormalised;
                moveTo = resolveTarget(motion, currentNormalised, resolveAnchor);
                moveElapsedMs = 0;
                moveDurationMs = Math.Max(1, motion.DurationMs);
                moveEasing = parseEasing(motion.Easing);
                return;
            }

            // wander (default for unknown modes that look like wander)
            if (string.Equals(mode, "wander", StringComparison.OrdinalIgnoreCase))
            {
                wanderTarget = pickWanderTarget(motion, currentNormalised);
                return;
            }

            Stop();
        }

        /// <summary>
        /// Advances motion. Returns the new normalised centre, or null when inactive.
        /// </summary>
        public Vector2? Update(double elapsedMs, Vector2 currentNormalised, EzPetMotionDefinition? definitionOverride = null)
        {
            var motion = definitionOverride ?? active;
            if (motion == null)
                return null;

            string mode = motion.Mode.Trim();

            if (string.Equals(mode, "teleportTo", StringComparison.OrdinalIgnoreCase))
            {
                var dest = moveTo;
                Stop();
                return dest;
            }

            if (string.Equals(mode, "moveTo", StringComparison.OrdinalIgnoreCase))
            {
                moveElapsedMs += elapsedMs;
                float t = (float)Math.Clamp(moveElapsedMs / moveDurationMs, 0, 1);
                float eased = (float)Interpolation.ApplyEasing(moveEasing, t);
                var pos = moveFrom + (moveTo - moveFrom) * eased;

                if (t >= 1)
                    Stop();

                return pos;
            }

            if (string.Equals(mode, "wander", StringComparison.OrdinalIgnoreCase))
            {
                float speed = Math.Max(0.001f, motion.Speed);
                float step = speed * (float)(elapsedMs / 1000.0);
                var delta = wanderTarget - currentNormalised;
                float dist = delta.Length;

                if (dist <= step || dist < 0.001f)
                {
                    wanderTarget = pickWanderTarget(motion, wanderTarget);
                    return wanderTarget;
                }

                return currentNormalised + delta / dist * step;
            }

            return null;
        }

        private Vector2 resolveTarget(EzPetMotionDefinition motion, Vector2 fallback, Func<string, Vector2?>? resolveAnchor)
        {
            if (!string.IsNullOrWhiteSpace(motion.Anchor) && resolveAnchor != null)
            {
                var anchored = resolveAnchor(motion.Anchor);
                if (anchored != null)
                    return clamp01(anchored.Value);
            }

            if (motion.Target is { Length: >= 2 })
                return clamp01(new Vector2(motion.Target[0], motion.Target[1]));

            return clamp01(fallback);
        }

        private Vector2 pickWanderTarget(EzPetMotionDefinition motion, Vector2 current)
        {
            getBounds(motion, out float x0, out float y0, out float x1, out float y1);

            for (int i = 0; i < 8; i++)
            {
                var candidate = new Vector2(
                    x0 + (float)random.NextDouble() * (x1 - x0),
                    y0 + (float)random.NextDouble() * (y1 - y0));

                if ((candidate - current).LengthSquared > 0.01f)
                    return candidate;
            }

            return new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        }

        private static void getBounds(EzPetMotionDefinition motion, out float x0, out float y0, out float x1, out float y1)
        {
            if (motion.Bounds is { Length: >= 4 })
            {
                x0 = Math.Clamp(motion.Bounds[0], 0f, 1f);
                y0 = Math.Clamp(motion.Bounds[1], 0f, 1f);
                x1 = Math.Clamp(motion.Bounds[2], 0f, 1f);
                y1 = Math.Clamp(motion.Bounds[3], 0f, 1f);

                if (x1 < x0)
                    (x0, x1) = (x1, x0);
                if (y1 < y0)
                    (y0, y1) = (y1, y0);

                return;
            }

            x0 = 0.1f;
            y0 = 0.2f;
            x1 = 0.9f;
            y1 = 0.85f;
        }

        private static Vector2 clamp01(Vector2 v) => new Vector2(Math.Clamp(v.X, 0f, 1f), Math.Clamp(v.Y, 0f, 1f));

        private static Easing parseEasing(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Easing.OutQuad;

            return Enum.TryParse(name, ignoreCase: true, out Easing easing) ? easing : Easing.OutQuad;
        }
    }
}
