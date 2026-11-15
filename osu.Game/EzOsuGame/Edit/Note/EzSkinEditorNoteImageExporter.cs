// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skinning;
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
            string? textureName = profile.ResolveTextureName(request.UseEzNoteVariants, EzManiaNotePart.Note, request.VariantId);

            if (textureName == null)
                return null;

            using var source = loadTextureImage(mountedSkinPath, textureName);

            if (source == null)
                return null;

            int width = Math.Max(1, (int)Math.Round(request.Width));
            int height = Math.Max(1, (int)Math.Round(request.Height));
            var result = new Image<Rgba32>(width, height);

            drawLayer(result, source, y: 0, width, height, request);

            return result;
        }

        private static Image<Rgba32>? exportHold(IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, string mountedSkinPath)
        {
            using var head = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzManiaNotePart.HoldHead, request.VariantId));
            using var body = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzManiaNotePart.HoldBody, request.VariantId));
            using var tail = loadTextureImage(mountedSkinPath, profile.ResolveTextureName(request.UseEzNoteVariants, EzManiaNotePart.HoldTail, request.VariantId));

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
                drawLayer(result, head, y, width, headHeight, request);
                y += headHeight;
            }

            if (body != null)
            {
                drawLayer(result, body, y, width, bodyHeight, request);
                y += bodyHeight;
            }

            if (tail != null)
                drawLayer(result, tail, y, width, tailHeight, request);

            return result;
        }

        private static void drawLayer(Image<Rgba32> result, Image<Rgba32> source, int y, int width, int height, EzSkinEditorNotePreviewRequest request)
        {
            result.Mutate(ctx =>
            {
                using var layer = source.Clone();
                layer.Mutate(c => c.Resize(width, height));
                applyTint(layer, request.NoteColour, request.ColouringMode, request.ColouringRange, request.ColouringStrength);
                ctx.DrawImage(layer, new Point(0, y), 1f);
            });
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

        private static void applyTint(Image<Rgba32> image, Colour4 tint, EzColouringMode mode, EzColouringRange range, double strength)
        {
            var algorithm = EzNoteRecolour.ToAlgorithm(mode);
            float colouringStrength = (float)strength;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        ref Rgba32 pixel = ref row[x];
                        var source = new Colour4(pixel.R / 255f, pixel.G / 255f, pixel.B / 255f, pixel.A / 255f);
                        var result = EzNoteRecolour.ApplyStraight(source, tint, algorithm, range, strength: colouringStrength);
                        pixel.R = toByte(result.R);
                        pixel.G = toByte(result.G);
                        pixel.B = toByte(result.B);
                        pixel.A = toByte(result.A);
                    }
                }
            });
        }

        private static byte toByte(float channel) => (byte)Math.Clamp((int)Math.Round(channel * 255f), 0, 255);
    }
}
