// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivJsonHelper
    {
        public static JToken UnwrapIllust(JToken token)
        {
            if (token is JObject obj && obj["illust"] is JObject nested)
                return nested;

            return token;
        }

        public static JToken? Field(JToken token, string snakeCaseName)
        {
            JToken? value = token[snakeCaseName];

            if (value != null)
                return value;

            return token[snakeToCamel(snakeCaseName)];
        }

        public static int IntValue(JToken token, string snakeCaseName)
        {
            JToken? value = Field(token, snakeCaseName);

            if (value == null)
                return 0;

            if (value.Type == JTokenType.Integer)
                return value.Value<int>();

            if (value.Type == JTokenType.String && int.TryParse(value.ToString(), out int parsed))
                return parsed;

            return 0;
        }

        public static long LongValue(JToken token, string snakeCaseName)
        {
            JToken? value = Field(token, snakeCaseName);

            if (value == null)
                return 0;

            if (value.Type == JTokenType.Integer)
                return value.Value<long>();

            if (value.Type == JTokenType.String && long.TryParse(value.ToString(), out long parsed))
                return parsed;

            return 0;
        }

        public static string? StringValue(JToken token, string snakeCaseName) => Field(token, snakeCaseName)?.ToString();

        public static string? ResolveNextUrl(JObject root)
        {
            foreach (var container in enumeratePayloads(root))
            {
                string? nextUrl = StringValue(container, "next_url");

                if (!string.IsNullOrWhiteSpace(nextUrl))
                    return nextUrl;
            }

            return null;
        }

        public static IReadOnlyList<JToken> ExtractIllustTokens(JObject root)
        {
            var results = new List<JToken>();

            foreach (var container in enumeratePayloads(root))
            {
                foreach (string key in new[] { "illusts", "home_ranking_illusts", "ranking_illusts", "thumbnail_illusts" })
                {
                    if (appendIllustTokens(Field(container, key), results))
                        return results;
                }

                if (Field(container, "user_previews") is JArray previews)
                {
                    foreach (var preview in previews)
                    {
                        if (appendIllustTokens(Field(preview, "illusts"), results))
                            return results;
                    }
                }
            }

            return results;
        }

        public static string DescribeTopLevelKeys(JObject root) => string.Join(", ", root.Properties().Select(property => property.Name));

        private static IEnumerable<JObject> enumeratePayloads(JObject root)
        {
            yield return root;

            if (Field(root, "response") is JObject response)
                yield return response;
        }

        private static bool appendIllustTokens(JToken? token, List<JToken> results)
        {
            if (token == null)
                return false;

            switch (token.Type)
            {
                case JTokenType.Array:
                    results.AddRange(token.Children());
                    return results.Count > 0;

                case JTokenType.Object:
                    results.AddRange(token.Children<JProperty>().Select(property => property.Value));
                    return results.Count > 0;
            }

            return false;
        }

        private static string snakeToCamel(string snake)
        {
            int underscore = snake.IndexOf('_');

            if (underscore < 0)
                return snake;

            var builder = new StringBuilder(snake.Length);
            int start = 0;

            while (underscore >= 0)
            {
                builder.Append(snake, start, underscore - start);

                if (underscore + 1 < snake.Length)
                    builder.Append(char.ToUpperInvariant(snake[underscore + 1]));

                start = underscore + 2;
                underscore = snake.IndexOf('_', start);
            }

            builder.Append(snake, start, snake.Length - start);
            return builder.ToString();
        }
    }
}
