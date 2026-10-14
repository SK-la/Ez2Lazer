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
        private readonly int judgeMode;
        private readonly int healthMode;

        public SubmitEzManiaScoreMetadataRequest(int judgeMode, int healthMode, long scoreId, int beatmapId)
        {
            this.judgeMode = judgeMode;
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
                JudgeMode = judgeMode,
                HealthMode = healthMode,
            }));

            return req;
        }

        private class EzManiaScoreMetadataPayload
        {
            [JsonProperty("judge_mode")]
            public int JudgeMode { get; set; }

            [JsonProperty("health_mode")]
            public int HealthMode { get; set; }
        }
    }
}
