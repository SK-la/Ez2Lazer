// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivAuthService
    {
        private readonly Storage storage;

        private string? cachedAccessToken;
        private DateTimeOffset accessTokenExpiresAt = DateTimeOffset.MinValue;
        private readonly object tokenLock = new object();

        public PixivAuthService(Storage storage)
        {
            this.storage = storage;
        }

        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(LoadRefreshToken());

        public string? LoadRefreshToken() => loadRefreshTokenFromFile();

        public void SaveRefreshToken(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token cannot be empty.", nameof(refreshToken));

            writeRefreshTokenToFile(refreshToken);
            invalidateAccessToken();
        }

        public void ClearRefreshToken()
        {
            invalidateAccessToken();

            try
            {
                if (storage.Exists(EzModifyPath.PIXIV_AUTH_FILE))
                    storage.Delete(EzModifyPath.PIXIV_AUTH_FILE);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to delete Pixiv auth file: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
            }
        }

        public bool TryRefreshAccessToken(out string? accessToken, out string? error)
        {
            lock (tokenLock)
            {
                if (!string.IsNullOrEmpty(cachedAccessToken) && DateTimeOffset.UtcNow < accessTokenExpiresAt.AddMinutes(-1))
                {
                    accessToken = cachedAccessToken;
                    error = null;
                    return true;
                }

                string? refreshToken = LoadRefreshToken();

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    accessToken = null;
                    error = "Pixiv refresh token is not configured. Run tools/GetPixivRefreshToken.ps1 or paste a token in settings.";
                    return false;
                }

                try
                {
                    using var request = createTokenRequest(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = refreshToken,
                        ["include_policy"] = "true",
                    });

                    request.Perform();

                    if (request.ResponseStatusCode != HttpStatusCode.OK)
                    {
                        accessToken = null;
                        error = request.GetResponseString() ?? "Pixiv token refresh failed.";
                        invalidateAccessToken();
                        return false;
                    }

                    var json = JObject.Parse(request.GetResponseString() ?? string.Empty);
                    cachedAccessToken = json["access_token"]?.ToString();
                    string? newRefresh = json["refresh_token"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(newRefresh) && newRefresh != refreshToken)
                        SaveRefreshToken(newRefresh);

                    int expiresIn = json["expires_in"]?.Value<int>() ?? 3600;
                    accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

                    if (string.IsNullOrWhiteSpace(cachedAccessToken))
                    {
                        accessToken = null;
                        error = "Pixiv token refresh returned an empty access token.";
                        invalidateAccessToken();
                        return false;
                    }

                    accessToken = cachedAccessToken;
                    error = null;
                    return true;
                }
                catch (Exception ex)
                {
                    accessToken = null;
                    error = ex.Message;
                    invalidateAccessToken();
                    return false;
                }
            }
        }

        public string? GetAccessToken()
        {
            return TryRefreshAccessToken(out string? accessToken, out _) ? accessToken : null;
        }

        private void invalidateAccessToken()
        {
            cachedAccessToken = null;
            accessTokenExpiresAt = DateTimeOffset.MinValue;
        }

        private string? loadRefreshTokenFromFile()
        {
            try
            {
                if (!storage.Exists(EzModifyPath.PIXIV_AUTH_FILE))
                    return null;

                using var stream = storage.GetStream(EzModifyPath.PIXIV_AUTH_FILE);
                using var reader = new StreamReader(stream);
                string content = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var auth = Newtonsoft.Json.JsonConvert.DeserializeObject<PixivAuthFile>(content);
                return string.IsNullOrWhiteSpace(auth?.RefreshToken) ? null : auth.RefreshToken;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read Pixiv auth file: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                return null;
            }
        }

        private void writeRefreshTokenToFile(string refreshToken)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(new PixivAuthFile { RefreshToken = refreshToken }, Newtonsoft.Json.Formatting.Indented);
                using var stream = storage.CreateFileSafely(EzModifyPath.PIXIV_AUTH_FILE);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to write Pixiv auth file: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
            }
        }

        private static Framework.IO.Network.WebRequest createTokenRequest(Dictionary<string, string> formData)
        {
            var request = new Framework.IO.Network.WebRequest(PixivConstants.AUTH_TOKEN_URL)
            {
                Method = HttpMethod.Post,
            };

            request.AddHeader("User-Agent", PixivConstants.USER_AGENT);
            request.AddHeader("App-OS", PixivConstants.APP_OS);
            request.AddHeader("App-OS-Version", PixivConstants.APP_OS_VERSION);
            request.AddHeader("App-Version", PixivConstants.APP_VERSION);
            request.AddParameter("client_id", PixivConstants.CLIENT_ID);
            request.AddParameter("client_secret", PixivConstants.CLIENT_SECRET);

            foreach (var pair in formData)
                request.AddParameter(pair.Key, pair.Value);

            return request;
        }
    }
}
