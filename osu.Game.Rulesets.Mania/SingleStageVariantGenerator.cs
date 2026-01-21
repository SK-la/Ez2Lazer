// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Input.Bindings;

namespace osu.Game.Rulesets.Mania
{
    public class SingleStageVariantGenerator
    {
        private readonly int variant;
        private readonly InputKey[] leftKeys;
        private readonly InputKey[] rightKeys;

        public SingleStageVariantGenerator(int variant)
        {
            this.variant = variant;

            // 10K is special because it expands towards the centre of the keyboard (V/N), rather than towards the edges of the keyboard.
            if (variant == 10)
            {
                leftKeys = new[] { InputKey.LControl, InputKey.A, InputKey.S, InputKey.D, InputKey.Space };
                rightKeys = new[] { InputKey.Slash, InputKey.L, InputKey.Semicolon, InputKey.Quote, InputKey.Enter };
            }
            else if (variant == 12)
            {
                leftKeys = new[] { InputKey.Tab, InputKey.LControl, InputKey.A, InputKey.S, InputKey.D, InputKey.Space };
                rightKeys = new[] { InputKey.Slash, InputKey.L, InputKey.Semicolon, InputKey.Quote, InputKey.Enter, InputKey.BackSlash };
            }
            else if (variant == 14)
            {
                leftKeys = new[] { InputKey.Tab, InputKey.LControl, InputKey.A, InputKey.S, InputKey.D, InputKey.Space, InputKey.G };
                rightKeys = new[] { InputKey.Slash, InputKey.L, InputKey.Semicolon, InputKey.Quote, InputKey.Enter, InputKey.BackSlash, InputKey.P };
            }
            else if (variant == 16)
            {
                leftKeys = new[] { InputKey.Tab, InputKey.LControl, InputKey.A, InputKey.S, InputKey.D, InputKey.Space, InputKey.Number5, InputKey.Number6 };
                rightKeys = new[] { InputKey.Number7, InputKey.Number8, InputKey.Slash, InputKey.L, InputKey.Semicolon, InputKey.Quote, InputKey.Enter, InputKey.BackSlash };
            }
            else // if (variant == 18)
            {
                leftKeys = new[] { InputKey.Q, InputKey.W, InputKey.E, InputKey.R, InputKey.A, InputKey.S, InputKey.D, InputKey.F, InputKey.Space };
                rightKeys = new[] { InputKey.Alt, InputKey.L, InputKey.Semicolon, InputKey.Quote, InputKey.Enter, InputKey.O, InputKey.P, InputKey.BracketLeft, InputKey.BracketRight };
            }
            // else
            // {
            //     leftKeys = new[] { InputKey.A, InputKey.S, InputKey.D, InputKey.F };
            //     rightKeys = new[] { InputKey.J, InputKey.K, InputKey.L, InputKey.Semicolon };
            // }
        }

        public IEnumerable<KeyBinding> GenerateMappings() => new VariantMappingGenerator
        {
            LeftKeys = leftKeys,
            RightKeys = rightKeys,
            SpecialKey = InputKey.Space,
        }.GenerateKeyBindingsFor(variant);
    }
}
