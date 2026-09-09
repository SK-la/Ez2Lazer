// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// SQLite persistence for <see cref="EzChartSkillInfo"/> keyed by beatmap hash
    /// (analysis DB table <c>chart_skill_info</c>; no Realm bump).
    /// </summary>
    public sealed class EzChartSkillInfoStore
    {
        private static readonly JsonSerializerOptions json_options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly EzAnalysisPersistentStore persistentStore;

        public EzChartSkillInfoStore(EzAnalysisPersistentStore persistentStore)
        {
            this.persistentStore = persistentStore;
        }

        public bool TryGet(string beatmapHash, out EzChartSkillInfo? info)
            => persistentStore.TryGetChartSkillInfo(beatmapHash, out info);

        public void Upsert(string beatmapHash, EzChartSkillInfo info)
            => persistentStore.UpsertChartSkillInfo(beatmapHash, info);

        internal static string Serialize(EzChartSkillInfo info)
            => JsonSerializer.Serialize(info, json_options);

        internal static bool TryDeserialize(string json, out EzChartSkillInfo? info)
        {
            try
            {
                info = JsonSerializer.Deserialize<EzChartSkillInfo>(json, json_options);
                return info != null;
            }
            catch
            {
                info = null;
                return false;
            }
        }
    }
}
