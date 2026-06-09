// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Structured view of a <c>skin.ini</c> file. Preserves unknown sections and non key-value lines.
    /// </summary>
    public sealed class EzSkinIniDocument
    {
        public const string GENERAL_SECTION = "General";
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
                    currentSection.UpsertKeyLine(key, value);
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
