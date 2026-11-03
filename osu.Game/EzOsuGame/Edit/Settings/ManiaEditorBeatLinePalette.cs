// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings
{
    /// <summary>
    /// Beat snap line palette for the mania editor (line B). Shares divisor keys with <see cref="BindableBeatDivisor"/>
    /// and note divisor tint source colours. Persisted in <c>BeatmapEditor.json</c> under <c>Colouring.BeatLines</c>.
    /// </summary>
    public sealed class ManiaEditorBeatLinePalette
    {
        public static readonly int[] EDITOR_LINE_DIVISORS = BindableBeatDivisor.PREDEFINED_DIVISORS;

        private readonly Dictionary<int, BindableColour4> bindables = new Dictionary<int, BindableColour4>();
        private readonly HashSet<int> storedDivisors = new HashSet<int>();

        public ManiaEditorBeatLinePalette()
        {
            foreach (int divisor in EDITOR_LINE_DIVISORS)
                bindables[divisor] = new BindableColour4(Colour4.White);
        }

        public static Color4 LineColour(int divisor, OsuColour colours, ManiaEditorBeatLinePalette? palette)
        {
            if (palette != null)
                return palette.GetLineColour(divisor, colours);

            return BindableBeatDivisor.GetColourFor(divisor, colours);
        }

        public BindableColour4 GetBindable(int divisor)
        {
            if (!bindables.TryGetValue(divisor, out var bindable))
            {
                bindable = new BindableColour4(Colour4.White);
                bindables[divisor] = bindable;
            }

            return bindable;
        }

        public Color4 GetLineColour(int beatDivisor, OsuColour fallbackColours)
            => GetBindable(beatDivisor).Value;

        public void EnsureDefaults(OsuColour colours)
        {
            foreach (int divisor in EDITOR_LINE_DIVISORS)
            {
                var bindable = GetBindable(divisor);
                var expected = (Colour4)BindableBeatDivisor.GetColourFor(divisor, colours);

                if (bindable.Value != expected && !storedDivisors.Contains(divisor))
                    bindable.Value = expected;
            }
        }

        internal void ApplyDocument(Dictionary<string, string>? document)
        {
            storedDivisors.Clear();

            if (document == null)
                return;

            foreach (var pair in document)
            {
                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int divisor))
                    continue;

                if (!TryParseStoredColour(pair.Value, out var colour))
                    continue;

                GetBindable(divisor).Value = colour;
                storedDivisors.Add(divisor);
            }
        }

        internal Dictionary<string, string> ToDocument()
        {
            var payload = new Dictionary<string, string>();

            foreach (var (divisor, bindable) in bindables)
                payload[divisor.ToString(CultureInfo.InvariantCulture)] = bindable.Value.ToHex();

            return payload;
        }

        internal IEnumerable<BindableColour4> AllBindables => bindables.Values;

        private static bool TryParseStoredColour(string? value, out Colour4 colour)
        {
            colour = Colour4.White;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (EzSkinIniColourFormat.TryParse(value, out colour))
                return true;

            try
            {
                colour = Colour4.FromHex(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
