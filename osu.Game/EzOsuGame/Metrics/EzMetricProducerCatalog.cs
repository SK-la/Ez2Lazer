// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Shared machinery for the two producer catalogs: identity derivation from member names, and the
    /// enum-to-declaration table built once by reflection.
    /// </summary>
    internal static class EzMetricProducerNames
    {
        /// <summary>Suffix that keeps a player producer's id distinct from the chart producer it reads.</summary>
        public const string PLAYER_SUFFIX = "_player";

        /// <summary>
        /// Converts a member name to its producer id: <c>OfficialPerformance</c> becomes <c>official_performance</c>.
        /// </summary>
        /// <remarks>
        /// Deriving the id instead of writing it as a literal means a member cannot carry two names. The conversion
        /// is mechanical, so a test can lock every derived id without a parallel table of expected strings.
        /// </remarks>
        public static string ToId(string memberName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

            var builder = new StringBuilder(memberName.Length + 8);

            for (int i = 0; i < memberName.Length; i++)
            {
                char c = memberName[i];

                if (char.IsUpper(c) && i > 0)
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Builds enum-to-declaration tables from <see cref="EzMetricProducerMetaAttribute"/> on each field.
    /// </summary>
    internal static class EzMetricProducerMetaCache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly TEnum[] ALL;
        public static readonly IReadOnlyDictionary<TEnum, EzMetricProducerMetaAttribute> BY_VALUE;
        public static readonly IReadOnlyDictionary<string, TEnum> BY_ID;

        static EzMetricProducerMetaCache()
        {
            var values = Enum.GetValues<TEnum>();
            var byValue = new Dictionary<TEnum, EzMetricProducerMetaAttribute>(values.Length);
            var byId = new Dictionary<string, TEnum>(StringComparer.Ordinal);

            foreach (var value in values)
            {
                string name = value.ToString();
                var field = typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static);
                var meta = field?.GetCustomAttribute<EzMetricProducerMetaAttribute>()
                           ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{name} is missing [EzMetricProducerMeta].");

                byValue[value] = meta;

                if (!byId.TryAdd(EzMetricProducerNames.ToId(name), value))
                    throw new InvalidOperationException($"Duplicate metric producer id '{EzMetricProducerNames.ToId(name)}' on {typeof(TEnum).Name}.");
            }

            ALL = values;
            BY_VALUE = new ReadOnlyDictionary<TEnum, EzMetricProducerMetaAttribute>(byValue);
            BY_ID = new ReadOnlyDictionary<string, TEnum>(byId);
        }

        public static EzMetricProducerMetaAttribute Meta(TEnum value) => BY_VALUE[value];

        public static bool TryParse(string? id, out TEnum value)
        {
            if (!string.IsNullOrEmpty(id) && BY_ID.TryGetValue(id, out value))
                return true;

            value = default;
            return false;
        }
    }
}
