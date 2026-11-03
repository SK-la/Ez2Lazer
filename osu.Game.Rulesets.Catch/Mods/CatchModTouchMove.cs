// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Catch.Mods
{
    /// <summary>
    /// Replaces button-based catcher movement with direct positioning: the catcher follows a dragging finger, or
    /// moves by the mouse travel distance scaled by <see cref="ModTouchBase.MoveMapping"/>.
    /// </summary>
    /// <remarks>
    /// The ruleset's dash acceleration and the beatmap-driven hyper-dash adaptation are disabled, as the catcher is
    /// positioned directly rather than moved at a ruleset-determined speed. While
    /// <see cref="ModTouchBase.InvertDash"/> is enabled, the dash button locks movement instead.
    /// </remarks>
    public partial class CatchModTouchMove : ModTouchBase, IApplicableToDrawableRuleset<CatchHitObject>, IApplicableToPlayer
    {
        public override Type[] IncompatibleMods => new[] { typeof(ModRelax), typeof(ModAutoplay), typeof(CatchModMovingFast) };

        private DrawableCatchRuleset drawableRuleset = null!;

        public void ApplyToDrawableRuleset(DrawableRuleset<CatchHitObject> drawableRuleset)
        {
            this.drawableRuleset = (DrawableCatchRuleset)drawableRuleset;
        }

        public void ApplyToPlayer(Player player)
        {
            if (drawableRuleset.HasReplayLoaded.Value)
                return;

            var catchPlayfield = (CatchPlayfield)drawableRuleset.Playfield;
            catchPlayfield.Catcher.TouchControlled = true;

            // Overlays live inside the ruleset input manager, so the mapper receives both bindings and touch/mouse.
            drawableRuleset.Overlays.Add(new CatchTouchMoveInputMapper(catchPlayfield, this));
        }

        /// <summary>
        /// Translates touch and mouse input into catcher positions.
        /// </summary>
        private partial class CatchTouchMoveInputMapper : TouchMoveInputMapper<CatchAction>
        {
            private readonly CatchPlayfield playfield;
            private readonly CatchModTouchMove mod;

            public CatchTouchMoveInputMapper(CatchPlayfield playfield, CatchModTouchMove mod)
            {
                this.playfield = playfield;
                this.mod = mod;
            }

            protected override bool MovementLocked => mod.InvertDash.Value && DashHeld;

            protected override TouchMoveAction ClassifyAction(CatchAction action)
            {
                switch (action)
                {
                    case CatchAction.MoveLeft:
                        return TouchMoveAction.MoveLeft;

                    case CatchAction.MoveRight:
                        return TouchMoveAction.MoveRight;

                    case CatchAction.Dash:
                        return TouchMoveAction.Dash;

                    default:
                        return TouchMoveAction.None;
                }
            }

            protected override void OnTouchPositionChanged(float screenSpaceX)
            {
                float playfieldX = playfield.ScreenSpaceToGamefield(new Vector2(screenSpaceX, 0)).X;
                playfield.CatcherArea.SetCatcherPosition(playfieldX);
            }

            protected override void OnMouseMoveBy(float screenSpaceDeltaX)
            {
                float playfieldWidth = playfield.ScreenSpaceDrawQuad.Width;

                if (playfieldWidth <= 0)
                    return;

                // While the emergency stop is disabled, dash keeps its original meaning and speeds movement up.
                double acceleration = mod.InvertDash.Value ? 1 : Catcher.BASE_DASH_SPEED / Catcher.BASE_WALK_SPEED;
                float playfieldDelta = (float)(screenSpaceDeltaX * mod.MoveMapping.Value * acceleration) * (CatchPlayfield.WIDTH / playfieldWidth);

                playfield.CatcherArea.SetCatcherPosition(playfield.CatcherArea.Catcher.X + playfieldDelta);
            }
        }
    }
}
