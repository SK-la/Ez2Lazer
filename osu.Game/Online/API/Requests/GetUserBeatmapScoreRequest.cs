// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// GET /beatmaps/{beatmap}/scores/users/{user}
    /// </summary>
    public class GetUserBeatmapScoreRequest : APIRequest<APIScoreWithPosition>
    {
        private readonly int beatmapId;
        private readonly long userId;
        private readonly RulesetInfo? ruleset;

        public GetUserBeatmapScoreRequest(int beatmapId, long userId, RulesetInfo? ruleset = null)
        {
            this.beatmapId = beatmapId;
            this.userId = userId;
            this.ruleset = ruleset;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            if (ruleset != null)
                req.AddParameter("mode", ruleset.ShortName);

            return req;
        }

        protected override string Target => $@"beatmaps/{beatmapId}/scores/users/{userId}";
    }
}
