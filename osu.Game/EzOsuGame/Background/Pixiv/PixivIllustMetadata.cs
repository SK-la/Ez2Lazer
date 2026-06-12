// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Newtonsoft.Json;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivIllustMetadata
    {
        private const string sidecar_extension = ".pixiv.json";

        public static string GetSidecarPath(string resourcePath)
            => Path.ChangeExtension(resourcePath, sidecar_extension);

        public static void Write(Storage storage, string resourcePath, PixivIllustInfo illust)
        {
            string sidecarPath = GetSidecarPath(resourcePath);
            string payload = JsonConvert.SerializeObject(new PixivIllustMetaFile
            {
                Account = illust.Account,
                UserName = illust.UserName,
            });

            using var stream = storage.CreateFileSafely(sidecarPath);
            using var writer = new StreamWriter(stream);
            writer.Write(payload);
        }

        public static bool TryRead(Storage storage, string resourcePath, out string account, out string userName)
        {
            account = string.Empty;
            userName = string.Empty;
            string sidecarPath = GetSidecarPath(resourcePath);

            if (!storage.Exists(sidecarPath))
                return false;

            try
            {
                using var stream = storage.GetStream(sidecarPath);
                using var reader = new StreamReader(stream);
                var meta = JsonConvert.DeserializeObject<PixivIllustMetaFile>(reader.ReadToEnd());

                if (meta == null)
                    return false;

                account = PixivAccountNormalizer.Normalize(meta.Account);
                userName = PixivAccountNormalizer.Normalize(meta.UserName);
                return account.Length > 0 || userName.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private class PixivIllustMetaFile
        {
            [JsonProperty("account")]
            public string Account { get; set; } = string.Empty;

            [JsonProperty("userName")]
            public string UserName { get; set; } = string.Empty;
        }
    }
}
