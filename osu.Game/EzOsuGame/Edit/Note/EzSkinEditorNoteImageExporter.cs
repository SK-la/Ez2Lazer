// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Imaging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skinning;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public static class EzSkinEditorNoteImageExporter
    {
        public static RgbaImage? Export(
            IEzSkinEditorNoteRulesetProfile profile,
            EzSkinEditorNotePreviewRequest request,
            string mountedSkinPath)
        {
            if (request.CompareKind == EzSkinEditorNoteCompareKind.Hold)
                return exportHold(profile, request, mountedSkinPath);

            return exportTap(profile, request, mountedSkinPath);
        }

        private static RgbaImage? exportTap(IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, string mountedSkinPath)
        {
            string? textureName = profile.ResolveTextureName(request.UseEzNoteVariants, EzManiaNotePart.Note, request.VariantId);

            if (textureName == null)
                return null;

            using var source = loadTextureImage(mountedSkinPath, textureName);

            if (source == null)
                return null;

            int width = Math.Max(1, (int)Math.Round(request.Width));
            int height = Math.Max(1, (int)Math.Round(request.Height));
            var result = new RgbaImage(width, height);

            drawLayer(result, source, y: 0, width, height, request);

            return result;
        }

        private static RgbaImage? exportHold(IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, string mountedSkinPath)
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

            var result = new RgbaImage(width, height);
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

        private static void drawLayer(RgbaImage result, RgbaImage source, int y, int width, int height, EzSkinEditorNotePreviewRequest request)
        {
            using var layer = source.Resize(width, height);
            applyTint(layer, request.NoteColour, request.ColouringMode, request.ColouringRange, request.ColouringStrength);
            ImageProcessing.DrawImage(result, layer, 0, y);
        }

        private static RgbaImage? loadTextureImage(string mountedSkinPath, string? textureName)
        {
            if (textureName == null)
                return null;

            string filePath = Path.Combine(mountedSkinPath, textureName.Replace('/', Path.DirectorySeparatorChar) + ".png");

            if (!File.Exists(filePath))
                return null;

            using var stream = File.OpenRead(filePath);
            return (ImageCodec.Decoder ?? throw new InvalidOperationException("No image decoder has been registered.")).Decode(stream);
        }

        private static void applyTint(RgbaImage image, Colour4 tint, EzColouringMode mode, EzColouringRange range, double strength)
        {
            var algorithm = EzNoteRecolour.ToAlgorithm(mode);
            float whiteTolerance = (float)strength / 255f;

            foreach (ref Rgba32 pixel in image.PixelSpan)
            {
                var source = new Colour4(pixel.R / 255f, pixel.G / 255f, pixel.B / 255f, pixel.A / 255f);
                var result = EzNoteRecolour.ApplyStraight(source, tint, algorithm, range, whiteTolerance: whiteTolerance);
                pixel.R = toByte(result.R);
                pixel.G = toByte(result.G);
                pixel.B = toByte(result.B);
                pixel.A = toByte(result.A);
            }
        }

        private static byte toByte(float channel) => (byte)Math.Clamp((int)Math.Round(channel * 255f), 0, 255);
    }
}
