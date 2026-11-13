// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    public class LegacySkinExporter : LegacyArchiveExporter<SkinInfo>
    {
        public LegacySkinExporter(Storage storage)
            : base(storage)
        {
        }

        protected override IEnumerable<(string Filename, Stream? Contents)> EnumerateExportEntries(SkinInfo model)
        {
            if (!EzLegacySkinFolders.TryGetDirectory(model, out string directory))
                return base.EnumerateExportEntries(model);

            return enumerateFolder(directory);
        }

        private static IEnumerable<(string Filename, Stream? Contents)> enumerateFolder(string directory)
        {
            IEnumerable<string> paths;

            try
            {
                paths = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToList();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Export legacy skin folder failed.");
                yield break;
            }

            foreach (string path in paths)
            {
                string name = Path.GetRelativePath(directory, path).Replace('\\', '/');

                Stream? contents = null;

                try
                {
                    contents = File.OpenRead(path);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Exporting skip lose file:s {name}。");
                }

                yield return (name, contents);
            }
        }

        protected override bool UseFixedEncoding => false;

        protected override string FileExtension => @".osk";
    }
}
