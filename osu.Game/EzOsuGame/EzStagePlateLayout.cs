// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // 坐标是 GLB 像素、Y 朝上。画到 osu 时 X、Y 都取反。
    internal sealed class EzStagePlateLayout
    {
        [JsonPropertyName("body")]
        public EzStagePlateSprite? Body { get; set; }

        [JsonPropertyName("back")]
        public EzStagePlateSprite? Back { get; set; }

        [JsonPropertyName("grooveLight")]
        public EzStagePlateSprite? GrooveLight { get; set; }

        [JsonPropertyName("meter")]
        public EzStagePlateMeter? Meter { get; set; }

        // 判定线贴图。Y 不在这里给，由 ratio 换算，见 EzStagePlateJudgementLine。
        [JsonPropertyName("judgementLine")]
        public EzStagePlateJudgementLine? JudgementLine { get; set; }

        [JsonPropertyName("overObject")]
        public EzStagePlateSprite? OverObject { get; set; }

        [JsonPropertyName("character")]
        public EzStagePlateSprite? Character { get; set; }

        // path 相对 Stage/{舞台名}。共用一套数字时是 number，分开时是 number/score 与 number/combo。
        [JsonPropertyName("score")]
        public EzStagePlateDigits? Score { get; set; }

        // 舞台上的最大连击，不是 HUD 当前连击。path 同样来自 prefab。
        [JsonPropertyName("combo")]
        public EzStagePlateDigits? Combo { get; set; }

        // 固定槽位之外的图，按 path 加载，不按皮肤名分支。
        [JsonPropertyName("parts")]
        public List<EzStagePlateSprite>? Parts { get; set; }

        /// <summary>
        /// 舞台贴图的设计宽度。列宽到舞台缩放以此为准，允许自制舞台用其它宽度。
        /// </summary>
        [JsonPropertyName("designWidth")]
        public float DesignWidth { get; set; } = EzStagePlacement.DEFAULT_DESIGN_WIDTH;

        /// <summary>
        /// 按键动画（keybase 与 keypress）的播放帧率。缺省或非正数时用
        /// <see cref="DEFAULT_KEY_PRESS_FPS"/>，与旧固定帧长一致。
        /// </summary>
        [JsonPropertyName("keyPressFps")]
        public float KeyPressFps { get; set; }

        /// <summary>
        /// 旧实现给按键动画的固定帧长是 <c>1000 / 60 * 4 * 4</c> 毫秒，换算成 3.75 FPS。
        /// </summary>
        public const float DEFAULT_KEY_PRESS_FPS = 3.75f;

        /// <summary>
        /// 按键位置，键为键数（"4"、"5"…）。
        /// </summary>
        /// <remarks>
        /// 整幅画布型美术（一张图里已按列画好）在 prefab 里各列 x 都是 0，因此这里也全是 0；
        /// 小块平铺型则记录每列中心。两种形态都靠这一组坐标定位，绘制时不再按列宽二次缩放。
        /// </remarks>
        [JsonPropertyName("keys")]
        public Dictionary<string, List<EzStagePlateKey>>? Keys { get; set; }

        /// <summary>
        /// 某键数的按键槽位；未声明时返回 <c>null</c>。
        /// </summary>
        public IReadOnlyList<EzStagePlateKey>? GetKeys(int keyMode)
        {
            if (Keys == null || !Keys.TryGetValue(keyMode.ToString(CultureInfo.InvariantCulture), out List<EzStagePlateKey>? keys))
                return null;

            return keys.Count > 0 ? keys : null;
        }

        public static EzStagePlateLayout? TryLoad(EzResourceStore resource, string stageName)
        {
            using (Stream? stream = resource.GetEzResourceStream($"Stage/{stageName}/Stage/layout.json"))
            {
                if (stream == null)
                    return null;

                try
                {
                    EzStagePlateLayout? layout = JsonSerializer.Deserialize<EzStagePlateLayout>(stream);
                    return layout?.Meter == null ? null : layout;
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// 舞台判定线。贴图底边贴合该位置，位置由 <see cref="Ratio"/> 在 Body 图内的归一化高度决定。
    /// </summary>
    /// <remarks>
    /// <see cref="Ratio"/> 从 Body 图底边往上量，0 是底边、1 是顶边，与 Body 的分辨率无关。
    /// 它同时是舞台板的缩放锚点：舞台按命中判定线摆放后，任何列宽/缩放都不再移动这条线。
    /// </remarks>
    internal sealed class EzStagePlateJudgementLine
    {
        /// <summary>
        /// 246 / 1080：Ez2ON 舞台图中底边到判定线的实测比例。layout 未给出时为该值。
        /// </summary>
        public const float DEFAULT_RATIO = 246f / 1080f;

        // 相对 Stage/{舞台名}/Stage。缺省 JudgementLine。
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        // 与其它 stage 组件同一坐标系（Unity X，绘制时取反）。
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("ratio")]
        public float Ratio { get; set; } = DEFAULT_RATIO;

        // 多帧时的帧间隔（毫秒）。缺省沿用舞台动画的 66。
        [JsonPropertyName("frameMs")]
        public double FrameMs { get; set; }

        public double FrameLength => FrameMs > 0 ? FrameMs : 66;
    }

    /// <summary>
    /// 一列的按键槽位。坐标与其它 stage 组件同一套（Unity X，绘制时取反）。
    /// </summary>
    internal sealed class EzStagePlateKey
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        /// <summary>
        /// 该列的底图名前缀（不含扩展名与帧序号），如 <c>1KeyBase</c>。
        /// </summary>
        /// <remarks>
        /// 不记录帧序号：同一列的 <c>{前缀}_0</c>、<c>{前缀}_1</c>… 由取图逻辑按前缀试探补齐，
        /// 因此增删帧、改帧序号宽度都不需要改 layout。
        /// </remarks>
        [JsonPropertyName("base")]
        public string? Base { get; set; }

        /// <summary>
        /// 该列的按下图文件名，规则同 <see cref="Base"/>。
        /// </summary>
        [JsonPropertyName("press")]
        public string? Press { get; set; }
    }

    internal sealed class EzStagePlateMeter
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("gauge")]
        public EzStagePlateSprite? Gauge { get; set; }

        [JsonPropertyName("bright")]
        public EzStagePlateSprite? Bright { get; set; }

        [JsonPropertyName("mask")]
        public EzStagePlateMask? Mask { get; set; }
    }

    internal sealed class EzStagePlateMask
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("scaleY")]
        public float ScaleY { get; set; }
    }

    // 个位锚点与 body 同一坐标系。高一位在 x + spacing。
    internal sealed class EzStagePlateDigits
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        // 相对 Stage/{舞台名}。共用一套时是 number，分开时是 number/score、number/combo。
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("digits")]
        public int Digits { get; set; }

        [JsonPropertyName("spacing")]
        public float Spacing { get; set; }

        public Vector2 ToOsuPosition() => EzStagePlateSprite.ToOsu(X, Y);
    }

    internal sealed class EzStagePlateSprite
    {
        // 相对 Stage/{舞台}/Stage 的文件名。缺省时沿用该槽位原来的固定路径。
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("blend")]
        public string? Blend { get; set; }

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }

        [JsonPropertyName("frameMs")]
        public double FrameMs { get; set; }

        // 正角度为 Unity 的 +Z，屏幕上逆时针。seconds 是转过 degrees 的时间。
        [JsonPropertyName("spin")]
        public EzStagePlateSpin? Spin { get; set; }

        public static Vector2 ToOsu(float x, float y) => new Vector2(-x, -y);

        public Vector2 ToOsuPosition() => ToOsu(X, Y);

        public bool IsAdditive => string.Equals(Blend, "additive", StringComparison.OrdinalIgnoreCase);

        public double FrameLength => FrameMs > 0 ? FrameMs : 66;
    }

    internal sealed class EzStagePlateSpin
    {
        [JsonPropertyName("seconds")]
        public double Seconds { get; set; }

        [JsonPropertyName("degrees")]
        public float Degrees { get; set; }

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }
    }
}
