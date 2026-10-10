// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osuTK;

namespace osu.Game.EzOsuGame.Animation
{
    /// <summary>
    /// 按控制器、片段名、<see cref="EzEnumGameThemeName"/> 查找已提取的动画模板。
    /// </summary>
    public static partial class EzAnimationLibrary
    {
        public const string JUDGEMENT = "Judgement";

        public const string COMBO = "Combo";

        public const string COMBO_NEW = "ComboAni_New";

        public const string COMBO_NEW_TITLE = "ComboAni_New_Title";

        public const string OVER_OBJECT = "OverObject";

        public const string JUDGEMENT_ANI = "JudgementAni";

        public const string JUDGEMENT_ANI_1 = "JudgementAni_1";

        public const string JUDGEMENT_ANI_KOOL = "JudgementAni_Kool";

        public const string JUDGEMENT_ANI_KOOL_1 = "JudgementAni_Kool_1";

        private static readonly Dictionary<TemplateId, EzAnimationTemplate> templates = createTemplates();

        /// <summary>
        /// 命中该主题下的控制器片段时返回模板。
        /// </summary>
        public static bool TryGet(string controller, string clip, EzEnumGameThemeName theme, out EzAnimationTemplate template)
            => templates.TryGetValue(new TemplateId(theme, controller, clip), out template);

        /// <summary>
        /// 把变形模板下拉项对应到已提取曲线的主题。内置项没有主题。
        /// </summary>
        public static bool TryGetDeformTheme(EzEnumDeformTemplate template, out EzEnumGameThemeName theme)
        {
            if (template == EzEnumDeformTemplate.BuiltIn || !Enum.TryParse(template.ToString(), out theme))
            {
                theme = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 播放该主题的 combo 出现动画。标题只播标题通道。
        /// </summary>
        public static bool TryPlayCombo(EzEnumDeformTemplate selection, Drawable drawable, bool title)
        {
            if (!TryGetDeformTheme(selection, out EzEnumGameThemeName theme))
                return false;

            string clip = title ? COMBO_NEW_TITLE : COMBO_NEW;

            if (!TryGet(COMBO, clip, theme, out EzAnimationTemplate template))
                return false;

            template.Play(drawable, true);
            return true;
        }

        private static Dictionary<TemplateId, EzAnimationTemplate> createTemplates()
        {
            var map = new Dictionary<TemplateId, EzAnimationTemplate>();
            populate(map);
            return map;
        }

        static partial void populate(Dictionary<TemplateId, EzAnimationTemplate> map);

        private static void add(Dictionary<TemplateId, EzAnimationTemplate> map, EzEnumGameThemeName theme, string controller, string clip, float[]? scaleX, float[]? scaleY, float[]? positionX, float[]? positionY, float[]? alpha)
        {
            map[new TemplateId(theme, controller, clip)] = new EzAnimationTemplate(
                EzAnimationChannel.FromPairs(scaleX),
                EzAnimationChannel.FromPairs(scaleY),
                EzAnimationChannel.FromPairs(positionX),
                EzAnimationChannel.FromPairs(positionY),
                EzAnimationChannel.FromPairs(alpha));
        }

        private readonly struct TemplateId : IEquatable<TemplateId>
        {
            public readonly EzEnumGameThemeName Theme;
            public readonly string Controller;
            public readonly string Clip;

            public TemplateId(EzEnumGameThemeName theme, string controller, string clip)
            {
                Theme = theme;
                Controller = controller;
                Clip = clip;
            }

            public bool Equals(TemplateId other)
                => Theme == other.Theme
                   && string.Equals(Controller, other.Controller, StringComparison.Ordinal)
                   && string.Equals(Clip, other.Clip, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is TemplateId other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Theme, Controller, Clip);
        }
    }

    /// <summary>
    /// 一段已归一化的动画。缩放 1 为静止尺寸，位移是相对静止位的画面坐标（Y 向下为正）。
    /// </summary>
    public readonly struct EzAnimationTemplate
    {
        public EzAnimationChannel ScaleX { get; }
        public EzAnimationChannel ScaleY { get; }
        public EzAnimationChannel PositionX { get; }
        public EzAnimationChannel PositionY { get; }
        public EzAnimationChannel Alpha { get; }

        public EzAnimationTemplate(EzAnimationChannel scaleX, EzAnimationChannel scaleY, EzAnimationChannel positionX, EzAnimationChannel positionY, EzAnimationChannel alpha)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            PositionX = positionX;
            PositionY = positionY;
            Alpha = alpha;
        }

        /// <summary>
        /// 按关键帧播放。多帧贴图传 <paramref name="includeAlpha"/> 为 false，避免盖住帧闪烁。
        /// </summary>
        public void Play(Drawable drawable, bool includeAlpha)
        {
            drawable.FinishTransforms();

            playScale(drawable);
            playPosition(drawable);

            if (includeAlpha && !Alpha.IsEmpty)
                playAlpha(drawable);
            else
                drawable.Alpha = 1;
        }

        private void playScale(Drawable drawable)
        {
            if (ScaleX.IsEmpty && ScaleY.IsEmpty)
                return;

            double[] times = mergeTimes(ScaleX, ScaleY);
            drawable.Scale = new Vector2(ScaleX.ValueAt(times[0], 1), ScaleY.ValueAt(times[0], 1));

            for (int i = 0; i < times.Length - 1; i++)
            {
                double start = times[i];
                double duration = times[i + 1] - start;
                var target = new Vector2(ScaleX.ValueAt(times[i + 1], 1), ScaleY.ValueAt(times[i + 1], 1));

                using (drawable.BeginDelayedSequence(toMilliseconds(start)))
                    drawable.ScaleTo(target, toMilliseconds(duration), Easing.None);
            }
        }

        private void playPosition(Drawable drawable)
        {
            if (PositionX.IsEmpty && PositionY.IsEmpty)
                return;

            double[] times = mergeTimes(PositionX, PositionY);
            drawable.Position = new Vector2(PositionX.ValueAt(times[0], 0), PositionY.ValueAt(times[0], 0));

            for (int i = 0; i < times.Length - 1; i++)
            {
                double start = times[i];
                double duration = times[i + 1] - start;
                var target = new Vector2(PositionX.ValueAt(times[i + 1], 0), PositionY.ValueAt(times[i + 1], 0));

                using (drawable.BeginDelayedSequence(toMilliseconds(start)))
                    drawable.MoveTo(target, toMilliseconds(duration), Easing.None);
            }
        }

        private void playAlpha(Drawable drawable)
        {
            EzAnimationKey[] keys = Alpha.Keys;
            drawable.Alpha = keys[0].Value;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                double start = keys[i].Time;
                double duration = keys[i + 1].Time - start;
                float target = keys[i + 1].Value;

                using (drawable.BeginDelayedSequence(toMilliseconds(start)))
                    drawable.FadeTo(target, toMilliseconds(duration), Easing.None);
            }
        }

        private const double milliseconds_per_second = 1000;

        private static double toMilliseconds(double seconds) => seconds * milliseconds_per_second;

        private static double[] mergeTimes(EzAnimationChannel first, EzAnimationChannel second)
        {
            var times = new List<double>(first.Keys.Length + second.Keys.Length);

            void collect(EzAnimationChannel channel)
            {
                for (int i = 0; i < channel.Keys.Length; i++)
                {
                    double time = channel.Keys[i].Time;

                    if (times.Count == 0 || times[^1] != time)
                        times.Add(time);
                }
            }

            collect(first);
            collect(second);
            times.Sort();

            int write = 0;

            for (int i = 0; i < times.Count; i++)
            {
                if (write == 0 || times[write - 1] != times[i])
                    times[write++] = times[i];
            }

            times.RemoveRange(write, times.Count - write);
            return times.ToArray();
        }
    }

    /// <summary>
    /// 一条通道上的关键帧。空通道表示这段动画不驱动该属性。
    /// </summary>
    public readonly struct EzAnimationChannel
    {
        public static readonly EzAnimationChannel Empty = new EzAnimationChannel(Array.Empty<EzAnimationKey>());

        public EzAnimationKey[] Keys { get; }

        public bool IsEmpty => Keys.Length == 0;

        public EzAnimationChannel(EzAnimationKey[] keys)
        {
            Keys = keys;
        }

        public float ValueAt(double time, float fallback)
        {
            if (Keys.Length == 0)
                return fallback;

            if (time <= Keys[0].Time)
                return Keys[0].Value;

            for (int i = 1; i < Keys.Length; i++)
            {
                if (time <= Keys[i].Time)
                    return Keys[i].Value;
            }

            return Keys[^1].Value;
        }

        internal static EzAnimationChannel FromPairs(float[]? pairs)
        {
            if (pairs == null || pairs.Length == 0)
                return Empty;

            var keys = new EzAnimationKey[pairs.Length / 2];

            for (int i = 0; i < keys.Length; i++)
                keys[i] = new EzAnimationKey(pairs[i * 2], pairs[i * 2 + 1]);

            return new EzAnimationChannel(keys);
        }
    }

    public readonly struct EzAnimationKey
    {
        public double Time { get; }
        public float Value { get; }

        public EzAnimationKey(double time, float value)
        {
            Time = time;
            Value = value;
        }
    }

    /// <summary>
    /// 强制变形使用的动画。内置是手写压扁，其余项与 <see cref="EzEnumGameThemeName"/> 同名。
    /// </summary>
    public enum EzEnumDeformTemplate
    {
        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_DEFORM_TEMPLATE_BUILTIN))]
        BuiltIn,

        // ReSharper disable InconsistentNaming
        EZ2DJ_1st,
        EZ2DJ_1stSE,
        EZ2DJ_2nd,
        EZ2DJ_3rd,
        EZ2DJ_4th,
        EZ2DJ_6th,
        EZ2DJ_7th,
        AIR,
        AZURE_EXPRESSION,
        Celeste_Lumiere,
        CV_CRAFT,
        D2D_Station,
        Dark_Concert,
        DJMAX,
        EC_1304,
        EC_Wheel,
        EVOLVE,
        EZ2ON,
        FIND_A_WAY,
        Fortress2,
        Fortress3_Future,
        Fortress3_Gear,
        Fortress3_Green,
        Fortress3_Modern,
        GC,
        GC_EZ,
        Gem,
        HX_1121,
        HX_STANDARD,
        JIYU,
        Kings,
        Limited,
        NIGHT_FALL,
        O2_A9100,
        O2_EA05,
        O2_Jam,
        Platinum,
        QTZ_01,
        QTZ_02,
        REBOOT,
        SG_701,
        SH_512,
        Star,
        TANOc,
        TANOc2,
        TECHNIKA,
        TIME_TRAVELER,
        TOMATO,
        Turtle,
        Various_Ways,
        ArcadeScore,
        // ReSharper restore InconsistentNaming
    }
}
