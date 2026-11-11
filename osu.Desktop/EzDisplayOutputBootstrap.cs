// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Desktop
{
    /// <summary>
    /// Reads display output preferences before <see cref="osu.Framework.HostOptions"/> is constructed.
    /// </summary>
    internal static class EzDisplayOutputBootstrap
    {
        private const string display_output_key = "DisplayOutputMode";
        private const string sdr_exposure_key = "DisplayOutputSdrExposure";
        private const string hdr_paper_white_key = "DisplayOutputHdrPaperWhiteNits";

        public readonly record struct StartupTone(float SdrExposure, float HdrPaperWhiteNits);

        public static StartupTone ReadStartupTone(string gameName)
        {
            float sdr = (float)(tryReadIniDouble(gameName, sdr_exposure_key) ?? 1.0);
            float hdr = (float)(tryReadIniDouble(gameName, hdr_paper_white_key) ?? 0.0);
            return new StartupTone(
                Math.Clamp(sdr, 0.5f, 2f),
                Math.Clamp(hdr, 0f, 10000f));
        }

        public static DisplayOutputMode ReadRequestedMode(string gameName)
        {
            var parsed = readRequestedMode(gameName);

            return parsed switch
            {
                EzDisplayOutputMode.Native10Bit => DisplayOutputMode.Native10Bit,
                EzDisplayOutputMode.Hdr10 => DisplayOutputMode.Hdr10,
                _ => DisplayOutputMode.Standard,
            };
        }

        public static void ApplyRendererEnvironment(DisplayOutputMode mode)
        {
            if (mode == DisplayOutputMode.Standard)
                return;

            // Renderer selection stays with the user. 10-bit/HDR present only runs when the chosen surface is already D3D11.
        }

        private static EzDisplayOutputMode? readRequestedMode(string gameName)
        {
            string? raw = tryReadIniString(gameName, display_output_key);

            if (raw == null)
                return null;

            return Enum.TryParse(raw, true, out EzDisplayOutputMode mode) ? mode : null;
        }

        private static string? tryReadIniString(string gameName, string key)
        {
            try
            {
                string? dataDirectory = resolveDataDirectory(gameName);

                if (dataDirectory == null)
                    return null;

                string configPath = Path.Combine(dataDirectory, "EzSkinSettings.ini");

                if (!File.Exists(configPath))
                    return null;

                foreach (string line in File.ReadLines(configPath))
                {
                    int separator = line.IndexOf('=');

                    if (separator < 0)
                        continue;

                    if (!line.AsSpan(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return line.AsSpan(separator + 1).Trim().ToString();
                }
            }
            catch
            {
            }

            return null;
        }

        private static double? tryReadIniDouble(string gameName, string key)
        {
            string? raw = tryReadIniString(gameName, key);

            if (raw == null)
                return null;

            return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }

        private static string? resolveDataDirectory(string gameName)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrEmpty(roaming))
                return null;

            string defaultDirectory = Path.Combine(roaming, gameName);
            string storageIni = Path.Combine(defaultDirectory, "storage.ini");

            if (!File.Exists(storageIni))
                return defaultDirectory;

            foreach (string line in File.ReadLines(storageIni))
            {
                int separator = line.IndexOf('=');

                if (separator < 0)
                    continue;

                if (!line.AsSpan(0, separator).Trim().Equals("FullPath", StringComparison.Ordinal))
                    continue;

                string value = line.AsSpan(separator + 1).Trim().ToString();

                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                    return value;
            }

            return defaultDirectory;
        }
    }
}
