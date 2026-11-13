// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Client mod-select layout at <c>EzResources/Config/ModLayout.json</c>. Not part of a theme layout set.
    /// </summary>
    public sealed class EzModLayoutDocument
    {
        public const string FILENAME = "ModLayout.json";

        public Dictionary<string, EzRulesetModLayout> Rulesets { get; set; } = new Dictionary<string, EzRulesetModLayout>();
    }

    public sealed class EzRulesetModLayout
    {
        public List<EzModGroupLayout> Groups { get; set; } = new List<EzModGroupLayout>();

        public List<string> Hidden { get; set; } = new List<string>();
    }

    public sealed class EzModGroupLayout
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Mod type name this group was seeded from. Null for a group the player created.</summary>
        public string? SeedType { get; set; }

        public List<string> Acronyms { get; set; } = new List<string>();
    }

    public readonly struct EzModLayoutMod
    {
        public string Acronym { get; }

        public ModType Type { get; }

        public EzModLayoutMod(string acronym, ModType type)
        {
            Acronym = acronym;
            Type = type;
        }
    }

    /// <summary>
    /// 分组只决定 mod 选择界面的摆放。隐藏名单不表示禁用。
    /// 没有存档时按 <see cref="ModType"/> 的固定列顺序生成。
    /// 已有存档里没出现、也没被隐藏的 mod，追加到仍存在的同类型种子组；种子组没了就进第一组，避免被自动隐藏。
    /// </summary>
    public static class EzModLayoutResolver
    {
        public static readonly ModType[] DefaultTypeOrder =
        {
            ModType.LA_Mod,
            ModType.CommunityMod,
            ModType.DifficultyReduction,
            ModType.DifficultyIncrease,
            ModType.Automation,
            ModType.Conversion,
            ModType.Fun,
        };

        public static EzRulesetModLayout Resolve(EzRulesetModLayout? saved, IReadOnlyList<EzModLayoutMod> available)
        {
            var ordered = new List<EzModLayoutMod>(available.Count);
            var known = new Dictionary<string, EzModLayoutMod>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in available)
            {
                if (string.IsNullOrEmpty(mod.Acronym) || known.ContainsKey(mod.Acronym))
                    continue;

                known.Add(mod.Acronym, mod);
                ordered.Add(mod);
            }

            if (saved == null)
                return createDefault(ordered);

            var result = new EzRulesetModLayout();
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in saved.Groups ?? Enumerable.Empty<EzModGroupLayout>())
            {
                var copy = new EzModGroupLayout
                {
                    Id = string.IsNullOrEmpty(group.Id) ? Guid.NewGuid().ToString("N") : group.Id,
                    Name = group.Name ?? string.Empty,
                    SeedType = group.SeedType,
                };

                foreach (var acronym in group.Acronyms ?? Enumerable.Empty<string>())
                {
                    if (!known.TryGetValue(acronym, out var mod) || !placed.Add(mod.Acronym))
                        continue;

                    copy.Acronyms.Add(mod.Acronym);
                }

                result.Groups.Add(copy);
            }

            foreach (var acronym in saved.Hidden ?? Enumerable.Empty<string>())
            {
                if (!known.TryGetValue(acronym, out var mod) || !placed.Add(mod.Acronym))
                    continue;

                result.Hidden.Add(mod.Acronym);
            }

            foreach (var mod in ordered)
            {
                if (placed.Contains(mod.Acronym))
                    continue;

                EzModGroupLayout? seed = null;

                for (int i = 0; i < result.Groups.Count; i++)
                {
                    if (result.Groups[i].SeedType == mod.Type.ToString())
                    {
                        seed = result.Groups[i];
                        break;
                    }
                }

                if (seed != null)
                {
                    seed.Acronyms.Add(mod.Acronym);
                    continue;
                }

                if (result.Groups.Count > 0)
                {
                    result.Groups[0].Acronyms.Add(mod.Acronym);
                    continue;
                }

                var created = createGroup(mod.Type);
                created.Acronyms.Add(mod.Acronym);
                result.Groups.Add(created);
            }

            return result;
        }

        public static void MoveMod(EzRulesetModLayout layout, string acronym, string? targetGroupId, int insertIndex)
        {
            foreach (var group in layout.Groups)
                group.Acronyms.RemoveAll(candidate => string.Equals(candidate, acronym, StringComparison.OrdinalIgnoreCase));

            layout.Hidden.RemoveAll(candidate => string.Equals(candidate, acronym, StringComparison.OrdinalIgnoreCase));

            if (targetGroupId == null)
            {
                layout.Hidden.Add(acronym);
                return;
            }

            EzModGroupLayout? target = null;

            for (int i = 0; i < layout.Groups.Count; i++)
            {
                if (layout.Groups[i].Id == targetGroupId)
                {
                    target = layout.Groups[i];
                    break;
                }
            }

            if (target == null)
            {
                layout.Hidden.Add(acronym);
                return;
            }

            int index = Math.Clamp(insertIndex, 0, target.Acronyms.Count);
            target.Acronyms.Insert(index, acronym);
        }

        public static void MoveGroup(EzRulesetModLayout layout, int from, int to)
        {
            if (from < 0 || from >= layout.Groups.Count)
                return;

            var group = layout.Groups[from];
            layout.Groups.RemoveAt(from);
            int index = Math.Clamp(to, 0, layout.Groups.Count);
            layout.Groups.Insert(index, group);
        }

        public static EzModGroupLayout AddGroup(EzRulesetModLayout layout, string baseName)
        {
            string name = baseName;
            int suffix = 2;

            while (layout.Groups.Any(group => string.Equals(group.Name, name, StringComparison.Ordinal)))
            {
                name = $"{baseName} {suffix}";
                suffix++;
            }

            var group = new EzModGroupLayout
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
            };

            layout.Groups.Add(group);
            return group;
        }

        public static void RemoveGroup(EzRulesetModLayout layout, string groupId)
        {
            int index = layout.Groups.FindIndex(group => group.Id == groupId);

            if (index < 0)
                return;

            var removed = layout.Groups[index];
            layout.Groups.RemoveAt(index);

            foreach (var acronym in removed.Acronyms)
            {
                if (layout.Hidden.Any(hidden => string.Equals(hidden, acronym, StringComparison.OrdinalIgnoreCase)))
                    continue;

                layout.Hidden.Add(acronym);
            }
        }

        public static void RenameGroup(EzRulesetModLayout layout, string groupId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            for (int i = 0; i < layout.Groups.Count; i++)
            {
                if (layout.Groups[i].Id != groupId)
                    continue;

                layout.Groups[i].Name = name.Trim();
                return;
            }
        }

        private static EzRulesetModLayout createDefault(List<EzModLayoutMod> ordered)
        {
            var result = new EzRulesetModLayout();
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in DefaultTypeOrder)
            {
                var group = createGroup(type);

                foreach (var mod in ordered)
                {
                    if (mod.Type != type || !placed.Add(mod.Acronym))
                        continue;

                    group.Acronyms.Add(mod.Acronym);
                }

                if (group.Acronyms.Count > 0)
                    result.Groups.Add(group);
            }

            foreach (var mod in ordered)
            {
                if (!placed.Add(mod.Acronym))
                    continue;

                var group = createGroup(mod.Type);
                group.Acronyms.Add(mod.Acronym);
                result.Groups.Add(group);
            }

            return result;
        }

        private static EzModGroupLayout createGroup(ModType type) => new EzModGroupLayout
        {
            Id = $"type:{(int)type}",
            Name = type.Humanize(LetterCasing.Title),
            SeedType = type.ToString(),
        };
    }
}
