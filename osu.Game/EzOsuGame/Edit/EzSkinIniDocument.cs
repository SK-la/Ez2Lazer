// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Structured view of a <c>skin.ini</c> file. Preserves unknown sections and non key-value lines.
    /// </summary>
    public sealed class EzSkinIniDocument
    {
        public const string GENERAL_SECTION = "General";
        public const string COLOURS_SECTION = "Colours";
        public const string MANIA_SECTION = "Mania";

        private readonly List<EzSkinIniSection> sections = new List<EzSkinIniSection>();

        public IReadOnlyList<EzSkinIniSection> Sections => sections;

        public static EzSkinIniDocument Parse(string? text)
        {
            var document = new EzSkinIniDocument();
            EzSkinIniSection? currentSection = null;

            if (string.IsNullOrEmpty(text))
            {
                document.ensureSection(GENERAL_SECTION);
                return document;
            }

            foreach (ReadOnlySpan<char> rawLine in text.AsSpan().EnumerateLines())
            {
                string line = rawLine.ToString();

                if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
                {
                    string sectionName = line[1..^1];
                    currentSection = document.ensureSection(sectionName);
                    continue;
                }

                if (currentSection == null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    currentSection = document.ensureSection(GENERAL_SECTION);
                }

                if (trySplitKeyValue(line, out string key, out string value))
                {
                    // Mania blocks are anchored by repeated Keys lines and must preserve order.
                    if (currentSection.Name == MANIA_SECTION)
                        currentSection.Lines.Add(new EzSkinIniKeyLine(key, value));
                    else
                        currentSection.UpsertKeyLine(key, value);
                }
                else
                    currentSection.Lines.Add(new EzSkinIniRawLine(line));
            }

            document.ensureSection(GENERAL_SECTION);
            return document;
        }

        public string? GetValue(string sectionName, string key)
        {
            var section = findSection(sectionName);
            return section?.GetValue(key);
        }

        public void SetValue(string sectionName, string key, string value)
        {
            ensureSection(sectionName).UpsertKeyLine(key, value);
        }

        public string? GetColourValue(string key) => GetValue(COLOURS_SECTION, key);

        public bool TryGetColourValue(string key, out Colour4 colour)
        {
            if (TryParseColourValue(GetColourValue(key), out colour))
                return true;

            colour = Colour4.White;
            return false;
        }

        public void SetColourValue(string key, Colour4 colour, bool includeAlpha = false) => SetValue(COLOURS_SECTION, key, EzSkinIniColourFormat.ToIniString(colour, includeAlpha));

        public static bool TryParseColourValue(string? value, out Colour4 colour) => EzSkinIniColourFormat.TryParse(value, out colour);

        public IReadOnlyList<int> GetManiaKeys()
        {
            parseManiaSection(out _, out var blocks);
            var keys = new List<int>(blocks.Count);

            foreach (var block in blocks)
                keys.Add(block.Keys);

            return keys;
        }

        public string? GetManiaValue(int keys, string key)
        {
            parseManiaSection(out var preamble, out var blocks);

            foreach (var block in blocks)
            {
                if (block.Keys != keys)
                    continue;

                return getLineValue(block.Lines, key);
            }

            return keys == 0 ? getLineValue(preamble, key) : null;
        }

        public void SetManiaValue(int keys, string key, string value)
        {
            parseManiaSection(out var preamble, out var blocks);
            var block = findOrCreateManiaBlock(blocks, keys);
            upsertLineValue(block.Lines, key, value);
            rebuildManiaSection(preamble, blocks);
        }

        public EzSkinIniManiaBlock EnsureManiaBlock(int keys)
        {
            parseManiaSection(out var preamble, out var blocks);
            var block = findOrCreateManiaBlock(blocks, keys);
            rebuildManiaSection(preamble, blocks);
            return block;
        }

        public string Serialize()
        {
            var builder = new StringBuilder();

            for (int i = 0; i < sections.Count; i++)
            {
                if (i > 0)
                    builder.AppendLine();

                sections[i].WriteTo(builder);
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        private EzSkinIniSection ensureSection(string sectionName)
        {
            var section = findSection(sectionName);

            if (section != null)
                return section;

            section = new EzSkinIniSection(sectionName);
            sections.Add(section);
            return section;
        }

        private EzSkinIniSection? findSection(string sectionName)
        {
            foreach (var section in sections)
            {
                if (string.Equals(section.Name, sectionName, StringComparison.Ordinal))
                    return section;
            }

            return null;
        }

        private void parseManiaSection(out List<IEzSkinIniLine> preamble, out List<EzSkinIniManiaBlock> blocks)
        {
            preamble = new List<IEzSkinIniLine>();
            blocks = new List<EzSkinIniManiaBlock>();
            var section = findSection(MANIA_SECTION);

            if (section == null)
                return;

            EzSkinIniManiaBlock? currentBlock = null;

            foreach (var line in section.Lines)
            {
                if (line is EzSkinIniKeyLine { Key: "Keys" } keysLine
                    && int.TryParse(keysLine.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keys))
                {
                    currentBlock = new EzSkinIniManiaBlock(keys);
                    blocks.Add(currentBlock);
                    continue;
                }

                if (currentBlock == null)
                    preamble.Add(line);
                else
                    currentBlock.Lines.Add(line);
            }
        }

        private void rebuildManiaSection(List<IEzSkinIniLine> preamble, List<EzSkinIniManiaBlock> blocks)
        {
            var section = ensureSection(MANIA_SECTION);
            section.Lines.Clear();
            section.Lines.AddRange(preamble);

            foreach (var block in blocks)
            {
                section.Lines.Add(new EzSkinIniKeyLine("Keys", block.Keys.ToString(CultureInfo.InvariantCulture)));
                section.Lines.AddRange(block.Lines);
            }
        }

        private static EzSkinIniManiaBlock findOrCreateManiaBlock(List<EzSkinIniManiaBlock> blocks, int keys)
        {
            foreach (var block in blocks)
            {
                if (block.Keys == keys)
                    return block;
            }

            var created = new EzSkinIniManiaBlock(keys);
            blocks.Add(created);
            return created;
        }

        private static string? getLineValue(List<IEzSkinIniLine> lines, string key)
        {
            foreach (var line in lines)
            {
                if (line is EzSkinIniKeyLine keyLine && string.Equals(keyLine.Key, key, StringComparison.Ordinal))
                    return keyLine.Value;
            }

            return null;
        }

        private static void upsertLineValue(List<IEzSkinIniLine> lines, string key, string value)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] is EzSkinIniKeyLine keyLine && string.Equals(keyLine.Key, key, StringComparison.Ordinal))
                {
                    lines[i] = new EzSkinIniKeyLine(key, value);
                    return;
                }
            }

            lines.Add(new EzSkinIniKeyLine(key, value));
        }

        private static bool trySplitKeyValue(string line, out string key, out string value)
        {
            int separator = line.IndexOf(':');

            if (separator <= 0)
            {
                key = string.Empty;
                value = string.Empty;
                return false;
            }

            key = line[..separator].Trim();
            value = line[(separator + 1)..].TrimStart();
            return key.Length > 0;
        }
    }

    public sealed class EzSkinIniManiaBlock
    {
        public int Keys { get; }

        public List<IEzSkinIniLine> Lines { get; } = new List<IEzSkinIniLine>();

        public EzSkinIniManiaBlock(int keys) => Keys = keys;
    }

    public sealed class EzSkinIniSection
    {
        public string Name { get; }

        public List<IEzSkinIniLine> Lines { get; } = new List<IEzSkinIniLine>();

        public EzSkinIniSection(string name) => Name = name;

        public string? GetValue(string key)
        {
            foreach (var line in Lines)
            {
                if (line is EzSkinIniKeyLine keyLine && string.Equals(keyLine.Key, key, StringComparison.Ordinal))
                    return keyLine.Value;
            }

            return null;
        }

        public void UpsertKeyLine(string key, string value)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i] is EzSkinIniKeyLine keyLine && string.Equals(keyLine.Key, key, StringComparison.Ordinal))
                {
                    Lines[i] = new EzSkinIniKeyLine(key, value);
                    return;
                }
            }

            Lines.Add(new EzSkinIniKeyLine(key, value));
        }

        public void WriteTo(StringBuilder builder)
        {
            builder.Append('[').Append(Name).AppendLine("]");

            foreach (var line in Lines)
                builder.AppendLine(line.ToString());
        }
    }

    public interface IEzSkinIniLine
    {
        string ToString();
    }

    public sealed class EzSkinIniKeyLine : IEzSkinIniLine
    {
        public string Key { get; }
        public string Value { get; }

        public EzSkinIniKeyLine(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public override string ToString() => $"{Key}: {Value}";
    }

    public sealed class EzSkinIniRawLine : IEzSkinIniLine
    {
        public string Text { get; }

        public EzSkinIniRawLine(string text) => Text = text;

        public override string ToString() => Text;
    }
}
