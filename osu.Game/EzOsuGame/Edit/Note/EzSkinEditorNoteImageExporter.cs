// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Graphics;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public static class EzSkinEditorNoteImageExporter
    {
        public static Image<Rgba32>? Export(
            IEzSkinEditorNoteRulesetProfile profile,
            EzSkinEditorNotePreviewRequest request,
            string mountedSkinPath)
        {
            if (request.CompareKind == EzSkinEditorNoteCompareKind.Hold)
                return exportHold(profile, request, mountedSkinPath);

            return exportTap(profile, request, mountedSkinPath);
        }

        private static Image<Rgba32>? exportTap(IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, string mountedSkinPath)
        {
            string? textureName = profile.ResolveTextureName(request.UseEzNoteVariants, EzSkinEditorNotePart.Note, request.VariantId);

            if (textureName == null)
                return null;

            using var source = loadTextureImage(mountedSkinPath, textureName);

            if (source == null)
                return null;

            int width = Math.Max(1, (int)Math.Round(request.Width));
            int height = Math.Max(1, (int)Math.Round(request.Height));
            var result = new Image<Rgba32>(width, height);

            source.Mutate(c => c.Resize(width, height));
            applyTint(source, request.NoteColour);
            result.Mutate(c => c.DrawImage(source, new Point(0, 0), 1f));

            return result;
        }

        private static Image<Rgba32>? exportHold(IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, string mountedSkinPath)
        {
            using var head = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzSkinEditorNotePart.HoldHead, request.VariantId));
            using var body = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzSkinEditorNotePart.HoldBody, request.VariantId));
            using var tail = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzSkinEditorNotePart.HoldTail, request.VariantId));

            if (head == null && body == null && tail == null)
                return null;

            int width = Math.Max(1, (int)Math.Round(request.Width));
            int height = Math.Max(1, (int)Math.Round(request.Height));
            int headHeight = head?.Height ?? 24;
            int tailHeight = tail?.Height ?? 24;
            int bodyHeight = Math.Max(1, height - headHeight - tailHeight);

            var result = new Image<Rgba32>(width, height);
            int y = 0;

            if (head != null)
            {
                head.Mutate(c => c.Resize(width, headHeight));
                applyTint(head, request.NoteColour);
                result.Mutate(c => c.DrawImage(head, new Point(0, y), 1f));
                y += headHeight;
            }

            if (body != null)
            {
                body.Mutate(c => c.Resize(width, bodyHeight));
                applyTint(body, request.NoteColour);
                result.Mutate(c => c.DrawImage(body, new Point(0, y), 1f));
                y += bodyHeight;
            }

            if (tail != null)
            {
                tail.Mutate(c => c.Resize(width, tailHeight));
                applyTint(tail, request.NoteColour);
                result.Mutate(c => c.DrawImage(tail, new Point(0, y), 1f));
            }

            return result;
        }

        private static Image<Rgba32>? loadTextureImage(string mountedSkinPath, string? textureName)
        {
            if (textureName == null)
                return null;

            string filePath = Path.Combine(mountedSkinPath, textureName.Replace('/', Path.DirectorySeparatorChar) + ".png");

            if (!File.Exists(filePath))
                return null;

            return Image.Load<Rgba32>(filePath);
        }

        private static void applyTint(Image<Rgba32> image, Colour4 tint)
        {
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        ref Rgba32 pixel = ref row[x];
                        pixel.R = (byte)(pixel.R * tint.R);
                        pixel.G = (byte)(pixel.G * tint.G);
                        pixel.B = (byte)(pixel.B * tint.B);
                        pixel.A = (byte)(pixel.A * tint.A);
                    }
                }
            });
        }
    }
}
