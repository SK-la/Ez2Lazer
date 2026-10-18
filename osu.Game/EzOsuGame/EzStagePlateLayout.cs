// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Graphics;
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
        /// 小块平铺型则记录每列中心。两种形态都靠这一组坐标定位。
        /// 声明了 <see cref="KeysColumnWidth"/> 的键数，按键图宽度由列宽覆盖，不按源图尺寸。
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

        /// <summary>
        /// 各键数的按键是否按列宽缩放。键为键数（"7"、"9"…），值为 <c>true</c> 时该键数的按键图宽度跟随列宽。
        /// </summary>
        /// <remarks>
        /// 派生键数（7/9/10）借用别的键数目录的按键图，源图按源键数的间距做成，直接绘制会挤在一起。
        /// 未声明或为 <c>false</c> 时保持源图像素尺寸。
        /// </remarks>
        [JsonPropertyName("keysColumnWidth")]
        public Dictionary<string, bool>? KeysColumnWidth { get; set; }

        /// <summary>
        /// 该键数的按键图是否按列宽缩放；layout 未声明时为 <c>false</c>。
        /// </summary>
        public bool UsesColumnWidth(int keyMode)
            => KeysColumnWidth != null
               && KeysColumnWidth.TryGetValue(keyMode.ToString(CultureInfo.InvariantCulture), out bool enabled)
               && enabled;

        #region 图层默认顺序

        // layout 未声明 order 时的兜底。数值对齐 Unity prefab 里的 m_SortingOrder。
        public const int ORDER_BACK = 2;
        public const int ORDER_BODY = 10;
        public const int ORDER_OVER_OBJECT = 12;
        public const int ORDER_KEYS = 11;
        public const int ORDER_GROOVE_LIGHT = 12;
        public const int ORDER_METER = 12;
        public const int ORDER_CHARACTER = 13;
        public const int ORDER_DIGITS = 14;
        public const int ORDER_JUDGEMENT_LINE = 14;

        #endregion
    }

    /// <summary>
    /// 所有舞台部件共用的定位属性。JSON 里没有的字段按默认值忽略。
    /// </summary>
    /// <remarks>
    /// 坐标与 Body 同一套：GLB 像素、Y 朝上，绘制时 X、Y 都取反。
    /// 旋转默认绕该部件自身的位置，需要绕别的点时用 <see cref="PivotX"/> / <see cref="PivotY"/>。
    /// </remarks>
    internal class EzStagePlateElement
    {
        // 相对 Stage/{舞台}/Stage 的图或图集前缀。缺省时沿用该槽位原来的固定路径。
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        /// <summary>
        /// 静态旋转，单位为度，屏幕上顺时针为正（与 <see cref="Drawable.Rotation"/> 一致）。
        /// </summary>
        [JsonPropertyName("rotation")]
        public float Rotation { get; set; }

        /// <summary>
        /// 图层顺序，取自 Unity SpriteRenderer 的 <c>m_SortingOrder</c>：数值越大越靠前。
        /// 未声明时用该槽位的默认值。
        /// </summary>
        /// <remarks>
        /// 各舞台的层级并不统一（例如 GrooveLight 在 10~15 之间都有），所以顺序必须来自数据，
        /// 不能写死一套固定的绘制先后。
        /// </remarks>
        [JsonPropertyName("order")]
        public int? Order { get; set; }

        // 旋转支点。缺省时绕 (x, y) 自身旋转。
        [JsonPropertyName("pivotX")]
        public float? PivotX { get; set; }

        [JsonPropertyName("pivotY")]
        public float? PivotY { get; set; }

        [JsonIgnore]
        public bool HasRotation => Rotation != 0;

        /// <summary>
        /// 该部件在屏幕上应使用的旋转角（与 <see cref="Rotation"/> 同义，直接取用）。
        /// </summary>
        public float ToScreenRotation() => Rotation;

        public static Vector2 ToOsu(float x, float y) => new Vector2(-x, -y);

        public Vector2 ToOsuPosition() => ToOsu(X, Y);

        /// <summary>
        /// 旋转支点的 osu 坐标；未声明时为自身位置。
        /// </summary>
        public Vector2 ToOsuPivot()
            => PivotX.HasValue && PivotY.HasValue ? ToOsu(PivotX.Value, PivotY.Value) : ToOsuPosition();
    }

    /// <summary>
    /// 舞台判定线。贴图底边贴合该位置，位置由 <see cref="Ratio"/> 在 Body 图内的归一化高度决定。
    /// </summary>
    /// <remarks>
    /// <see cref="Ratio"/> 从 Body 图底边往上量，0 是底边、1 是顶边，与 Body 的分辨率无关。
    /// 它同时是舞台板的缩放锚点：舞台按命中判定线摆放后，任何列宽/缩放都不再移动这条线。
    /// </remarks>
    internal sealed class EzStagePlateJudgementLine : EzStagePlateElement
    {
        /// <summary>
        /// 246 / 1080：Ez2ON 舞台图中底边到判定线的实测比例。layout 未给出时为该值。
        /// </summary>
        public const float DEFAULT_RATIO = 246f / 1080f;

        [JsonPropertyName("ratio")]
        public float Ratio { get; set; } = DEFAULT_RATIO;

        // 多帧时的帧间隔（毫秒）。缺省沿用舞台动画的 66。
        [JsonPropertyName("frameMs")]
        public double FrameMs { get; set; }

        [JsonIgnore]
        public double FrameLength => FrameMs > 0 ? FrameMs : 66;
    }

    /// <summary>
    /// 一列的按键槽位。<see cref="EzStagePlateElement.Path"/> 是该列的底图前缀。
    /// </summary>
    /// <remarks>
    /// 按下图不单独记录：底图和按下图按同一模板命名（<c>KeyBase</c> / <c>KeyPress</c>），
    /// 由 <see cref="PressPath"/> 从底图前缀推导。命名不符的舞台就不显示按下层。
    /// </remarks>
    internal sealed class EzStagePlateKey : EzStagePlateElement
    {
        /// <summary>
        /// 该列的按下图前缀：把底图前缀里的 <c>Base</c> 换成 <c>Press</c>。
        /// </summary>
        /// <returns>无法推导时为 <c>null</c>。</returns>
        [JsonIgnore]
        public string? PressPath
        {
            get
            {
                string? path = Path?.Trim();

                if (string.IsNullOrEmpty(path))
                    return null;

                int index = path.LastIndexOf("Base", StringComparison.Ordinal);

                return index < 0 ? null : path[..index] + "Press" + path[(index + 4)..];
            }
        }
    }

    internal sealed class EzStagePlateMeter : EzStagePlateElement
    {
        [JsonPropertyName("gauge")]
        public EzStagePlateSprite? Gauge { get; set; }

        [JsonPropertyName("bright")]
        public EzStagePlateSprite? Bright { get; set; }

        [JsonPropertyName("mask")]
        public EzStagePlateMask? Mask { get; set; }
    }

    internal sealed class EzStagePlateMask : EzStagePlateElement
    {
        [JsonPropertyName("scaleY")]
        public float ScaleY { get; set; }
    }

    // 个位锚点与 body 同一坐标系。高一位在 x + spacing。
    internal sealed class EzStagePlateDigits : EzStagePlateElement
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("digits")]
        public int Digits { get; set; }

        [JsonPropertyName("spacing")]
        public float Spacing { get; set; }
    }

    internal sealed class EzStagePlateSprite : EzStagePlateElement
    {
        [JsonPropertyName("blend")]
        public string? Blend { get; set; }

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }

        [JsonPropertyName("frameMs")]
        public double FrameMs { get; set; }

        // 持续自转。正角度为 Unity 的 +Z，屏幕上逆时针。seconds 是转过 degrees 的时间。
        [JsonPropertyName("spin")]
        public EzStagePlateSpin? Spin { get; set; }

        [JsonIgnore]
        public bool IsAdditive => string.Equals(Blend, "additive", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
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
