// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.EzOsuGame.Online
{
    /// <summary>
    /// Uploads Ez2Lazer-specific score metadata to a custom server after the official score payload has been accepted.
    /// Official osu! servers ignore this endpoint; it is only intended for Ez-compatible backends.
    /// </summary>
    public class SubmitEzManiaScoreMetadataRequest : APIRequest
    {
        private readonly int beatmapId;
        private readonly long scoreId;
        private readonly int hitMode;
        private readonly int healthMode;

        public SubmitEzManiaScoreMetadataRequest(int hitMode, int healthMode, long scoreId, int beatmapId)
        {
            this.hitMode = hitMode;
            this.healthMode = healthMode;
            this.scoreId = scoreId;
            this.beatmapId = beatmapId;
        }

        protected override string Target => $@"beatmaps/{beatmapId}/solo/scores/{scoreId}/ez";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.ContentType = "application/json";
            req.Method = HttpMethod.Put;

            req.AddRaw(JsonConvert.SerializeObject(new EzManiaScoreMetadataPayload
            {
                ManiaHitMode = hitMode,
                ManiaHealthMode = healthMode,
            }));

            return req;
        }

        private class EzManiaScoreMetadataPayload
        {
            [JsonProperty("mania_hit_mode")]
            public int ManiaHitMode { get; set; }

            [JsonProperty("mania_health_mode")]
            public int ManiaHealthMode { get; set; }
        }
    }
}
