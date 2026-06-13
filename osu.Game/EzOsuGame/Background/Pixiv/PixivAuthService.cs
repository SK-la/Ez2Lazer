// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Localization;
using WebRequest = osu.Framework.IO.Network.WebRequest;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivAuthService
    {
        private readonly Storage storage;

        private string? cachedAccessToken;
        private string? cachedAccount;
        private DateTimeOffset accessTokenExpiresAt = DateTimeOffset.MinValue;
        private readonly object tokenLock = new object();

        public PixivAuthService(Storage storage)
        {
            this.storage = storage;
        }

        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(LoadRefreshToken());

        public string? LoadRefreshToken() => loadRefreshTokenFromFile();

        public string? LoadAccountName() => loadAuthFromFile()?.Account ?? cachedAccount;

        public void SaveRefreshToken(string refreshToken, string? account = null, bool invalidateAccessTokenCache = true)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token cannot be empty.", nameof(refreshToken));

            var existing = loadAuthFromFile();
            writeAuthToFile(new PixivAuthFile
            {
                RefreshToken = refreshToken,
                Account = account ?? existing?.Account ?? cachedAccount,
            });

            if (!string.IsNullOrWhiteSpace(account))
                cachedAccount = account;

            if (invalidateAccessTokenCache)
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

        public bool TryRefreshAccessToken(out string? accessToken, out LocalisableString? error)
        {
            lock (tokenLock)
            {
                if (!string.IsNullOrEmpty(cachedAccessToken) && DateTimeOffset.UtcNow < accessTokenExpiresAt.AddMinutes(-1))
                {
                    accessToken = cachedAccessToken;
                    error = null;
                    return true;
                }
            }

            string? refreshToken = LoadRefreshToken();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                accessToken = null;
                error = EzSettingsStrings.PIXIV_STATUS_NOT_CONFIGURED;
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
                    error = EzSettingsStrings.PIXIV_ERROR_TOKEN_REFRESH_FAILED;
                    Logger.Log($"[Pixiv] token refresh HTTP {request.ResponseStatusCode}: {request.GetResponseString()}", LoggingTarget.Network, LogLevel.Important);
                    invalidateAccessToken();
                    return false;
                }

                var json = JObject.Parse(request.GetResponseString() ?? string.Empty);
                var tokenPayload = PixivJsonHelper.Field(json, "response") as JObject ?? json;
                string? accessTokenFromResponse = PixivJsonHelper.StringValue(tokenPayload, "access_token");
                string? newRefresh = PixivJsonHelper.StringValue(tokenPayload, "refresh_token");

                string? responseAccount = PixivJsonHelper.Field(tokenPayload, "user")?["account"]?.ToString();

                if (!string.IsNullOrWhiteSpace(responseAccount))
                {
                    cachedAccount = responseAccount;
                    string tokenToStore = !string.IsNullOrWhiteSpace(newRefresh) ? newRefresh : refreshToken;
                    SaveRefreshToken(tokenToStore, responseAccount, invalidateAccessTokenCache: false);
                }
                else if (!string.IsNullOrWhiteSpace(newRefresh) && newRefresh != refreshToken)
                {
                    SaveRefreshToken(newRefresh, invalidateAccessTokenCache: false);
                }

                int expiresIn = json["expires_in"]?.Value<int>() ?? 3600;

                lock (tokenLock)
                {
                    accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
                    cachedAccessToken = accessTokenFromResponse;

                    if (string.IsNullOrWhiteSpace(cachedAccessToken))
                    {
                        accessToken = null;
                        error = EzSettingsStrings.PIXIV_ERROR_TOKEN_REFRESH_EMPTY;
                        invalidateAccessToken();
                        return false;
                    }

                    accessToken = cachedAccessToken;
                    error = null;
                    return true;
                }
            }
            catch (Exception ex)
            {
                accessToken = null;
                error = EzSettingsStrings.PIXIV_ERROR_REQUEST_FAILED;
                Logger.Log($"[Pixiv] token refresh: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                invalidateAccessToken();
                return false;
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

        private string? loadRefreshTokenFromFile() => loadAuthFromFile()?.RefreshToken;

        private PixivAuthFile? loadAuthFromFile()
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

                var auth = JsonConvert.DeserializeObject<PixivAuthFile>(content);

                if (auth == null || string.IsNullOrWhiteSpace(auth.RefreshToken))
                    return null;

                if (!string.IsNullOrWhiteSpace(auth.Account))
                    cachedAccount = auth.Account;

                return auth;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read Pixiv auth file: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                return null;
            }
        }

        private void writeAuthToFile(PixivAuthFile auth)
        {
            try
            {
                string json = JsonConvert.SerializeObject(auth, Formatting.Indented);
                using var stream = storage.CreateFileSafely(EzModifyPath.PIXIV_AUTH_FILE);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to write Pixiv auth file: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
            }
        }

        private static WebRequest createTokenRequest(Dictionary<string, string> formData)
        {
            var request = new WebRequest(PixivConstants.AUTH_TOKEN_URL)
            {
                Method = HttpMethod.Post,
            };

            PixivRequestHeaders.ApplyOAuthHeaders(request);
            request.AddParameter("client_id", PixivConstants.CLIENT_ID);
            request.AddParameter("client_secret", PixivConstants.CLIENT_SECRET);

            foreach (var pair in formData)
                request.AddParameter(pair.Key, pair.Value);

            PixivWebRequest.ConfigureApi(request);
            return request;
        }
    }
}
