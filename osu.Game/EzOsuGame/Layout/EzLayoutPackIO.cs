// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Import/export for sibling theme folders <c>layout</c> / <c>layout-*</c> under <see cref="EzModifyPath.CONFIG_ROOT_PATH"/>.
    /// </summary>
    public static class EzLayoutPackIO
    {
        public const string EXPORT_PACK_ROOT = "layout-packs";

        /// <summary>
        /// Copies every <c>*.json</c> in the active layout set to <c>{exportRoot}/layout-packs/{setName}/</c>.
        /// </summary>
        public static string ExportPack(Storage gameStorage, string layoutSetName, Storage exportRoot)
        {
            string relativeDest = $"{EXPORT_PACK_ROOT}/{layoutSetName}";
            var source = gameStorage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(layoutSetName));
            var destination = exportRoot.GetStorageForDirectory(relativeDest);
            copyJsonFiles(source, destination);
            return exportRoot.GetFullPath(relativeDest);
        }

        /// <summary>
        /// Copies every <c>*.json</c> from <paramref name="sourceDirectory"/> into a new <c>layout-{主题}</c> folder.
        /// </summary>
        public static string ImportPackFromDirectory(Storage gameStorage, string sourceDirectory, string themeSuffix)
        {
            string setName = EzLayoutSetNames.DirectoryNameForThemeSuffix(themeSuffix);
            var destination = gameStorage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(setName));

            foreach (string file in Directory.GetFiles(sourceDirectory, "*.json"))
            {
                string filename = Path.GetFileName(file);

                using (var input = File.OpenRead(file))
                using (var output = destination.CreateFileSafely(filename))
                    input.CopyTo(output);
            }

            return setName;
        }

        /// <summary>
        /// Exports one layout JSON from the active theme store to <c>{exportRoot}/layout-layers/{fileName}</c>.
        /// </summary>
        public static string ExportLayerJson(Storage gameStorage, EzConfigLayoutStore store, string layoutFile, Storage exportRoot)
        {
            var source = gameStorage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(store.LayoutSetName));
            string relativeDest = $"layout-layers/{layoutFile}";
            copySingleFile(source, exportRoot, layoutFile, relativeDest);
            return exportRoot.GetFullPath(relativeDest);
        }

        /// <summary>
        /// Replaces one layout JSON in the active theme store from an on-disk file.
        /// </summary>
        public static void ImportLayerJson(Storage gameStorage, EzConfigLayoutStore store, string layoutFile, string sourceFile)
        {
            var destination = gameStorage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(store.LayoutSetName));

            using (var input = File.OpenRead(sourceFile))
            using (var output = destination.CreateFileSafely(layoutFile))
                input.CopyTo(output);
        }

        private static void copyJsonFiles(Storage source, Storage destination)
        {
            foreach (string file in source.GetFiles(string.Empty, "*.json"))
                copySingleFile(source, destination, file, file);
        }

        private static void copySingleFile(Storage source, Storage destination, string sourceFile, string destFile)
        {
            if (!source.Exists(sourceFile))
                return;

            using (var input = source.GetStream(sourceFile))
            {
                if (input == null)
                    return;

                using (var output = destination.CreateFileSafely(destFile))
                    input.CopyTo(output);
            }
        }
    }
}
