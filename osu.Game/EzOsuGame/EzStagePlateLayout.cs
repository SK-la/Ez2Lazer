// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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

        [JsonPropertyName("overObject")]
        public EzStagePlateSprite? OverObject { get; set; }

        [JsonPropertyName("character")]
        public EzStagePlateSprite? Character { get; set; }

        // path 是主题内的字形目录，按 body prefab 的贴图来，不按槽位名猜。
        [JsonPropertyName("score")]
        public EzStagePlateDigits? Score { get; set; }

        // 舞台上的最大连击，不是 HUD 当前连击。path 同样来自 prefab。
        [JsonPropertyName("combo")]
        public EzStagePlateDigits? Combo { get; set; }

        // 固定槽位之外的图，按 path 加载，不按皮肤名分支。
        [JsonPropertyName("parts")]
        public List<EzStagePlateSprite>? Parts { get; set; }

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

        // 相对 GameTheme/{主题} 的字形目录。共用一套数字时是 number，分开时才是 number/score、number/combo、number/maxcombo。
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
