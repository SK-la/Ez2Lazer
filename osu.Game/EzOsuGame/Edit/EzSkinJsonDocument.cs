// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Per-skin Ez settings stored as <c>EzSkin.json</c> in skin storage.
    /// </summary>
    public sealed class EzSkinJsonDocument
    {
        public const int CURRENT_SCHEMA_VERSION = 1;

        public const string FILENAME = "EzSkin.json";

        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

        [JsonPropertyName("settings")]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        private static readonly JsonSerializerOptions serializer_options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static EzSkinJsonDocument Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new EzSkinJsonDocument();

            var document = JsonSerializer.Deserialize<EzSkinJsonDocument>(json, serializer_options) ?? new EzSkinJsonDocument();

            return document;
        }

        public string Serialize() => JsonSerializer.Serialize(this, serializer_options);

        public static string SerializeNormalized(EzSkinJsonDocument document)
        {
            var clone = new EzSkinJsonDocument
            {
                SchemaVersion = document.SchemaVersion,
                Settings = new Dictionary<string, string>(document.Settings, StringComparer.Ordinal),
            };

            return JsonSerializer.Serialize(clone, serializer_options);
        }
    }
}
