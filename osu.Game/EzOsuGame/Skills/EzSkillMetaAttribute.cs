// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Metadata attached to skill-related enum fields. Lists are derived via <see cref="Enum.GetValues{TEnum}"/> —
    /// do not maintain parallel All/ids/chips arrays.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EzSkillMetaAttribute : Attribute
    {
        public EzSkillMetaAttribute(string id, string nameZh, string nameEn, string accentHex = "#8f6bd8")
        {
            Id = id;
            NameZh = nameZh;
            NameEn = nameEn;
            AccentHex = accentHex;
        }

        public string Id { get; }

        public string NameZh { get; }

        public string NameEn { get; }

        public string AccentHex { get; }

        /// <summary>Radar / DominantAxis inclusion (Overall = false).</summary>
        public bool InRadar { get; init; } = true;

        public LocalisableString DisplayName => new EzLocalizationManager.EzLocalisableString(NameZh, NameEn);

        public EzSkillChip Chip => new(DisplayName, AccentHex);
    }

    /// <summary>Builds enum→meta tables once from <see cref="EzSkillMetaAttribute"/> on each field.</summary>
    internal static class EzEnumMetaCache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly TEnum[] All;
        public static readonly IReadOnlyDictionary<TEnum, EzSkillMetaAttribute> ByValue;
        public static readonly IReadOnlyDictionary<string, TEnum> ById;

        static EzEnumMetaCache()
        {
            var values = Enum.GetValues<TEnum>();
            var byValue = new Dictionary<TEnum, EzSkillMetaAttribute>(values.Length);
            var byId = new Dictionary<string, TEnum>(StringComparer.Ordinal);

            foreach (var value in values)
            {
                string name = value.ToString();
                var field = typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static);
                var meta = field?.GetCustomAttribute<EzSkillMetaAttribute>()
                           ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{name} is missing [EzSkillMeta].");

                byValue[value] = meta;

                if (!byId.TryAdd(meta.Id, value))
                    throw new InvalidOperationException($"Duplicate skill meta id '{meta.Id}' on {typeof(TEnum).Name}.");
            }

            All = values;
            ByValue = new ReadOnlyDictionary<TEnum, EzSkillMetaAttribute>(byValue);
            ById = new ReadOnlyDictionary<string, TEnum>(byId);
        }

        public static EzSkillMetaAttribute Meta(TEnum value) => ByValue[value];

        public static bool TryParse(string? id, out TEnum value)
        {
            if (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out value))
                return true;

            value = default;
            return false;
        }

        public static bool TryParseIgnoreCase(string? id, out TEnum value)
        {
            value = default;

            if (string.IsNullOrEmpty(id))
                return false;

            if (ById.TryGetValue(id, out value))
                return true;

            foreach (var kvp in ById)
            {
                if (string.Equals(kvp.Key, id, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
