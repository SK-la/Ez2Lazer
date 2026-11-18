// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Humanizer;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Client mod-select layout at <c>EzResources/Config/ModLayout.json</c>. Not part of a theme layout set.
    /// <see cref="ColumnShear"/> is global; <see cref="Rulesets"/> holds per-ruleset group/hidden lists keyed by <see cref="RulesetInfo.ShortName"/>.
    /// </summary>
    public sealed class EzModLayoutDocument
    {
        public const string FILENAME = "ModLayout.json";

        /// <summary>Horizontal column shear. <c>null</c> means <see cref="EzModLayoutResolver.DEFAULT_COLUMN_SHEAR"/>.</summary>
        public float? ColumnShear { get; set; }

        public Dictionary<string, EzModRulesetListLayout>? Rulesets { get; set; }

        public EzRulesetModLayout? ToLayout(string rulesetKey)
        {
            if (string.IsNullOrEmpty(rulesetKey))
                return null;

            bool hasShear = ColumnShear != null;
            EzModRulesetListLayout? list = null;

            if (Rulesets != null && Rulesets.TryGetValue(rulesetKey, out list)
                                 && ((list.Groups?.Count ?? 0) > 0 || (list.Hidden?.Count ?? 0) > 0))
            {
                list.EnsureMutableLists();
            }
            else
            {
                list = null;
            }

            if (!hasShear && list == null)
                return null;

            var result = new EzRulesetModLayout
            {
                ColumnShear = ColumnShear,
            };

            if (list != null)
            {
                result.Groups = list.Groups;
                result.Hidden = list.Hidden;
            }

            return result;
        }

        public void ApplyLayout(string rulesetKey, EzRulesetModLayout layout)
        {
            if (string.IsNullOrEmpty(rulesetKey))
                return;

            layout.EnsureMutableLists();
            ColumnShear = layout.ColumnShear;
            Rulesets ??= new Dictionary<string, EzModRulesetListLayout>(StringComparer.Ordinal);

            if (!Rulesets.TryGetValue(rulesetKey, out EzModRulesetListLayout? entry))
            {
                entry = new EzModRulesetListLayout();
                Rulesets[rulesetKey] = entry;
            }

            entry.Groups = layout.Groups;
            entry.Hidden = layout.Hidden;
        }
    }

    /// <summary>Per-ruleset mod list layout persisted under <see cref="EzModLayoutDocument.Rulesets"/>.</summary>
    public sealed class EzModRulesetListLayout
    {
        public List<EzModGroupLayout>? Groups { get; set; }

        public List<string>? Hidden { get; set; }

        [MemberNotNull(nameof(Groups), nameof(Hidden))]
        public void EnsureMutableLists()
        {
            Groups ??= new List<EzModGroupLayout>();

            Hidden ??= new List<string>();

            foreach (var group in Groups)
                group.EnsureMutableLists();
        }
    }

    /// <summary>Resolved mod-select layout for one ruleset session (includes global shear when loaded from store).</summary>
    public sealed class EzRulesetModLayout
    {
        public float? ColumnShear { get; set; }

        public List<EzModGroupLayout>? Groups { get; set; }

        public List<string>? Hidden { get; set; }

        [MemberNotNull(nameof(Groups), nameof(Hidden))]
        public void EnsureMutableLists()
        {
            Groups ??= new List<EzModGroupLayout>();

            Hidden ??= new List<string>();

            foreach (var group in Groups)
                group.EnsureMutableLists();
        }
    }

    public sealed class EzModGroupLayout
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Mod type name this group was seeded from. Null for a group the player created.</summary>
        public string? SeedType { get; set; }

        public List<string>? Acronyms { get; set; }

        [MemberNotNull(nameof(Acronyms))]
        public void EnsureMutableLists()
        {
            Acronyms ??= new List<string>();
        }
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
        public const float DEFAULT_COLUMN_SHEAR = 0.2f;

        public static float NormaliseColumnShear(float? value) =>
            Math.Clamp(value ?? DEFAULT_COLUMN_SHEAR, 0, DEFAULT_COLUMN_SHEAR);

        public static readonly ModType[] DEFAULT_TYPE_ORDER =
        {
            ModType.LA_Mod,
            ModType.CommunityMod,
            ModType.DifficultyReduction,
            ModType.DifficultyIncrease,
            ModType.Automation,
            ModType.Conversion,
            ModType.Fun,
        };

        public static EzRulesetModLayout Resolve(EzRulesetModLayout? saved, IReadOnlyList<EzModLayoutMod> available, bool preserveUnavailable = false)
        {
            var ordered = new List<EzModLayoutMod>(available.Count);
            var known = new Dictionary<string, EzModLayoutMod>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in available)
            {
                if (string.IsNullOrEmpty(mod.Acronym) || !known.TryAdd(mod.Acronym, mod))
                    continue;

                ordered.Add(mod);
            }

            if (saved == null)
                return createDefault(ordered);

            saved.EnsureMutableLists();

            var result = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>(),
                Hidden = new List<string>(),
            };
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in saved.Groups)
            {
                group.EnsureMutableLists();

                var copy = new EzModGroupLayout
                {
                    Id = string.IsNullOrEmpty(group.Id) ? Guid.NewGuid().ToString("N") : group.Id,
                    Name = group.Name,
                    SeedType = group.SeedType,
                    Acronyms = new List<string>(),
                };
                copy.EnsureMutableLists();

                foreach (string acronym in group.Acronyms)
                {
                    if (!known.TryGetValue(acronym, out var mod))
                    {
                        if (!preserveUnavailable || !placed.Add(acronym))
                            continue;

                        copy.Acronyms.Add(acronym);
                        continue;
                    }

                    if (!placed.Add(mod.Acronym))
                        continue;

                    copy.Acronyms.Add(mod.Acronym);
                }

                result.Groups.Add(copy);
            }

            foreach (string acronym in saved.Hidden)
            {
                if (!known.TryGetValue(acronym, out var mod))
                {
                    if (!preserveUnavailable || !placed.Add(acronym))
                        continue;

                    result.Hidden.Add(acronym);
                    continue;
                }

                if (!placed.Add(mod.Acronym))
                    continue;

                result.Hidden.Add(mod.Acronym);
            }

            result.ColumnShear = saved.ColumnShear;

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
                    seed.EnsureMutableLists();
                    seed.Acronyms.Add(mod.Acronym);
                    continue;
                }

                if (result.Groups.Count > 0)
                {
                    var firstGroup = result.Groups[0];
                    firstGroup.EnsureMutableLists();
                    firstGroup.Acronyms.Add(mod.Acronym);
                    continue;
                }

                var created = createGroup(mod.Type);
                created.EnsureMutableLists();
                created.Acronyms.Add(mod.Acronym);
                result.Groups.Add(created);
            }

            result.EnsureMutableLists();
            return result;
        }

        public static void MoveMod(EzRulesetModLayout layout, string acronym, string? targetGroupId, int insertIndex)
        {
            layout.EnsureMutableLists();

            foreach (var group in layout.Groups)
            {
                group.EnsureMutableLists();
                group.Acronyms.RemoveAll(candidate => string.Equals(candidate, acronym, StringComparison.OrdinalIgnoreCase));
            }

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

            target.EnsureMutableLists();
            int index = Math.Clamp(insertIndex, 0, target.Acronyms.Count);
            target.Acronyms.Insert(index, acronym);
        }

        public static void MoveGroup(EzRulesetModLayout layout, int from, int to)
        {
            layout.EnsureMutableLists();

            if (from < 0 || from >= layout.Groups.Count)
                return;

            var group = layout.Groups[from];
            layout.Groups.RemoveAt(from);
            int index = Math.Clamp(to, 0, layout.Groups.Count);
            layout.Groups.Insert(index, group);
        }

        public static EzModGroupLayout AddGroup(EzRulesetModLayout layout, string baseName)
        {
            layout.EnsureMutableLists();

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
                Acronyms = new List<string>(),
            };

            layout.Groups.Add(group);
            return group;
        }

        public static void RemoveGroup(EzRulesetModLayout layout, string groupId)
        {
            layout.EnsureMutableLists();

            int index = layout.Groups.FindIndex(group => group.Id == groupId);

            if (index < 0)
                return;

            var removed = layout.Groups[index];
            layout.Groups.RemoveAt(index);
            removed.EnsureMutableLists();

            foreach (string acronym in removed.Acronyms)
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

            layout.EnsureMutableLists();

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
            var result = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>(),
                Hidden = new List<string>(),
            };
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in DEFAULT_TYPE_ORDER)
            {
                var group = createGroup(type);
                group.EnsureMutableLists();

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
                group.EnsureMutableLists();
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
            Acronyms = new List<string>(),
        };
    }
}
