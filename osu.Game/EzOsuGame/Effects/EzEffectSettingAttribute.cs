// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Effects
{
    /// <summary>
    /// 这项设置只在对应的内置动效被选中时出现。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EzEffectSettingAttribute : Attribute
    {
        public EzEffectType Effect { get; }

        public EzEffectSettingAttribute(EzEffectType effect)
        {
            Effect = effect;
        }

        public static bool IsActive(PropertyInfo property, EzEffectType effect)
        {
            var mark = property.GetCustomAttribute<EzEffectSettingAttribute>();
            return mark == null || mark.Effect == effect;
        }
    }
}
