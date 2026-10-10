// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // 坐标是 GLB 像素、Y 朝上。贴图按 ScaleAdjust 2 显示，所以画到 osu 时位置折半，并且 X 取反。贴图像素不翻转。
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

    internal sealed class EzStagePlateSprite
    {
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

        public static Vector2 ToOsu(float x, float y) => new Vector2(-x, -y);

        public Vector2 ToOsuPosition() => ToOsu(X, Y);

        public bool IsAdditive => string.Equals(Blend, "additive", StringComparison.OrdinalIgnoreCase);

        public double FrameLength => FrameMs > 0 ? FrameMs : 66;
    }
}
