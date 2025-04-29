// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public static class EzEffectHelper
    {
        public static void ApplyScaleAnimation(Drawable target, bool wasIncrease, bool wasMiss, float increaseFactor, float decreaseFactor, float increaseDuration, float decreaseDuration)
        {
            float newScaleValue = Math.Clamp(target.Scale.X * (wasIncrease ? increaseFactor : decreaseFactor), 0.5f, 3f);
            Vector2 newScale = new Vector2(newScaleValue);

            target
                .ScaleTo(newScale, increaseDuration, Easing.OutQuint)
                .Then()
                .ScaleTo(Vector2.One, decreaseDuration, Easing.OutQuint);

            if (wasMiss)
                target.FlashColour(Color4.Red, decreaseDuration, Easing.OutQuint);
        }

        public static void ApplyBounceAnimation(Drawable target, bool wasIncrease, bool wasMiss, float increaseFactor, float decreaseFactor, float increaseDuration, float decreaseDuration)
        {
            float factor = Math.Clamp(wasIncrease ? 10 * increaseFactor : -10 * decreaseFactor, -100f, 100f);

            target
                .MoveToY(factor, increaseDuration / 2, Easing.OutBounce)
                .Then()
                .MoveToY(0, decreaseDuration, Easing.OutBounce);

            if (wasMiss)
                target.FlashColour(Color4.Red, decreaseDuration, Easing.OutQuint);
        }
    }
}

// float factor = 0;
//
// switch (AnimationOrigin.Value)
// {
//     case OriginOptions.TopCentre:
//         factor = Math.Clamp(wasIncrease ? 10 * IncreaseScale.Value : -50, -100f, 100f); // 向下跳
//         break;
//
//     case OriginOptions.BottomCentre:
//         factor = Math.Clamp(wasIncrease ? -10 * IncreaseScale.Value : 50, -100f, 100f); // 向上跳
//         break;
//
//     case OriginOptions.Centre:
//         factor = Math.Clamp(wasIncrease ? 10 * IncreaseScale.Value : -10 * IncreaseScale.Value, -100f, 100f); // 上下跳
//         break;
// }
