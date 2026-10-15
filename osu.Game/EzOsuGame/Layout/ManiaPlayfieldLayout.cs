// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Mania playfield layout, stored next to the HUD layout in <c>EzResources/Config/layout/ManiaPlayfieldLayout.json</c>.
    /// Each concern owns a section, so adding one (stage scale, storyboard position/scale) never rewrites the others.
    /// </summary>
    public sealed class ManiaPlayfieldLayout
    {
        public const string FILENAME = @"ManiaPlayfieldLayout.json";

        [JsonPropertyName("stage")]
        public ManiaStageLayout Stage { get; set; } = new ManiaStageLayout();

        private static readonly JsonSerializerOptions serializer_options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public static ManiaPlayfieldLayout Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new ManiaPlayfieldLayout();

            try
            {
                return JsonSerializer.Deserialize<ManiaPlayfieldLayout>(json, serializer_options) ?? new ManiaPlayfieldLayout();
            }
            catch (JsonException)
            {
                return new ManiaPlayfieldLayout();
            }
        }

        public string Serialize() => JsonSerializer.Serialize(this, serializer_options);
    }

    /// <summary>
    /// Stage section. Offsets are normalised to -1..1; 0 keeps the stage centred. Positive X moves right, positive Y moves up.
    /// </summary>
    public sealed class ManiaStageLayout
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
    }
}
