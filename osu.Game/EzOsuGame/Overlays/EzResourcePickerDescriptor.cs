// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.HUD;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 一次资源选择会话：候选键、预览品类、选中写回回调。
    /// </summary>
    public sealed class EzResourcePickerDescriptor
    {
        public required LocalisableString Title { get; init; }

        public required EzResourcePickerCategory Category { get; init; }

        public required IReadOnlyList<string> Items { get; init; }

        public string? CurrentKey { get; init; }

        public required Action<string> Commit { get; init; }

        public static EzResourcePickerDescriptor ForGameTheme(Storage storage, Bindable<EzEnumGameThemeName> bindable)
        {
            var items = EzResourceDiscovery.ListGameThemeCandidates(storage);
            string current = bindable.Value.ToString();

            return new EzResourcePickerDescriptor
            {
                Title = @"GameTheme",
                Category = EzResourcePickerCategory.GameTheme,
                Items = items,
                CurrentKey = current,
                Commit = key =>
                {
                    if (Enum.TryParse(key, out EzEnumGameThemeName parsed))
                        bindable.Value = parsed;
                }
            };
        }

        public static EzResourcePickerDescriptor ForNoteSet(Storage storage, Bindable<string> bindable)
        {
            var items = EzResourceDiscovery.ListNoteSetCandidates(storage);

            return new EzResourcePickerDescriptor
            {
                Title = @"Note Set",
                Category = EzResourcePickerCategory.NoteSet,
                Items = items,
                CurrentKey = bindable.Value,
                Commit = key => bindable.Value = key
            };
        }

        public static EzResourcePickerDescriptor ForStage(Storage storage, Bindable<string> bindable)
        {
            var items = EzResourceDiscovery.ListStageCandidates(storage);

            return new EzResourcePickerDescriptor
            {
                Title = @"Stage",
                Category = EzResourcePickerCategory.Stage,
                Items = items,
                CurrentKey = bindable.Value,
                Commit = key => bindable.Value = key
            };
        }
    }
}
