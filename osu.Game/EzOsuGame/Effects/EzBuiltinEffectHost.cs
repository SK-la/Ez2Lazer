// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Effects
{
    /// <summary>
    /// 标记组件上的 <see cref="EzBuiltinEffectHost"/>。皮肤编辑器把宿主上的设置展开到同一面板，读写落在宿主实例上。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EzBuiltinEffectSourceAttribute : Attribute
    {
        /// <summary>
        /// 类型下拉写入皮肤时用的名字。空则用 <see cref="EzBuiltinEffectHost.EffectType"/>。
        /// </summary>
        public string? TypeStorageName { get; }

        public EzBuiltinEffectSourceAttribute(string? typeStorageName = null)
        {
            TypeStorageName = typeStorageName;
        }
    }

    /// <summary>
    /// 内置缩放和弹跳的设置与播放。组件只声明宿主并调用 <see cref="Play"/>。
    /// </summary>
    public sealed class EzBuiltinEffectHost : IScopedSkinSettings
    {
        public EzScaleEffect Scale { get; }

        public EzBounceEffect Bounce { get; }

        private readonly bool exposeEndFactor;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.EFFECT_TYPE_LABEL), nameof(EzHUDStrings.EFFECT_TYPE_DESCRIPTION))]
        public Bindable<EzEffectType> EffectType { get; } = new Bindable<EzEffectType>(EzEffectType.Scale);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.EFFECT_START_FACTOR_LABEL), nameof(EzHUDStrings.EFFECT_START_FACTOR_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectStartFactor => Scale.StartFactor;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.EFFECT_END_FACTOR_LABEL), nameof(EzHUDStrings.EFFECT_END_FACTOR_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectEndFactor => Scale.EndFactor;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.EFFECT_START_DURATION_LABEL), nameof(EzHUDStrings.EFFECT_START_DURATION_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectStartTime => Scale.StartTime;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.EFFECT_END_DURATION_LABEL), nameof(EzHUDStrings.EFFECT_END_DURATION_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectEndDuration => Scale.EndDuration;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_PEAK_LABEL), nameof(EzHUDStrings.BOUNCE_PEAK_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BouncePeak => Bounce.Peak;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_RETURN_LABEL), nameof(EzHUDStrings.BOUNCE_RETURN_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceReturnDuration => Bounce.ReturnDuration;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_EASING_LABEL), nameof(EzHUDStrings.BOUNCE_EASING_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public Bindable<Easing> BounceEasing => Bounce.Ease;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_OVERSHOOT_LABEL), nameof(EzHUDStrings.BOUNCE_OVERSHOOT_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceOvershoot => Bounce.Overshoot;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_START_ALPHA_LABEL), nameof(EzHUDStrings.BOUNCE_START_ALPHA_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceStartAlpha => Bounce.StartAlpha;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_ALPHA_LABEL), nameof(EzHUDStrings.BOUNCE_ALPHA_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceAlpha => Bounce.Alpha;

        private EzBuiltinEffectHost(EzScaleEffect scale, EzBounceEffect bounce, bool exposeEndFactor)
        {
            Scale = scale;
            Bounce = bounce;
            this.exposeEndFactor = exposeEndFactor;
        }

        public static EzBuiltinEffectHost ForDigits() => new EzBuiltinEffectHost(EzScaleEffect.ForCounter(), EzBounceEffect.ForDigits(), true);

        public static EzBuiltinEffectHost ForTitle() => new EzBuiltinEffectHost(EzScaleEffect.ForTitle(), EzBounceEffect.ForTitle(), false);

        public bool ExposeSetting(PropertyInfo property) => exposeEndFactor || property.Name != nameof(EffectEndFactor);

        public bool IsSettingActive(PropertyInfo property) => EzEffectSettingAttribute.IsActive(property, EffectType.Value);

        public void WatchSettingsScope(Action onChange) => EffectType.BindValueChanged(_ => onChange());

        public void Play(Drawable target, bool wasIncrease, bool wasMiss)
        {
            switch (EffectType.Value)
            {
                case EzEffectType.Scale:
                    Scale.Play(target, wasIncrease, wasMiss);
                    break;

                case EzEffectType.Bounce:
                    Bounce.Play(target, wasIncrease, wasMiss);
                    break;
            }
        }
    }
}
