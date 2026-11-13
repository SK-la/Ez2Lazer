// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Layout

{
    /// <summary>
    ///     Theme-scoped HUD JSON under <c>layout</c> / <c>layout-*</c>. <see cref="SelectSet" /> persists via
    ///     <see cref="Ez2Setting.LayoutSetName" />.
    /// </summary>
    public sealed class EzLayoutThemeHost

    {
        public EzConfigLayoutStore Store { get; }

        public ManiaPlayfieldLayoutConfig PlayfieldLayout => Store.PlayfieldLayout;

        public string CurrentSetName => Store.LayoutSetName;

        private readonly Bindable<string> layoutSetName;

        private bool suppressBindableCallback;

        public EzLayoutThemeHost(Storage gameStorage, Ez2ConfigManager config)

        {
            Store = new EzConfigLayoutStore(gameStorage);

            layoutSetName = config.GetBindable<string>(Ez2Setting.LayoutSetName);

            EzLayoutSetCandidates.EnsureInitialSelection(layoutSetName, gameStorage);

            Store.UseSet(EzLayoutSetNames.Normalize(layoutSetName.Value));

            layoutSetName.BindValueChanged(e =>

            {
                if (suppressBindableCallback)

                    return;

                Store.UseSet(EzLayoutSetNames.Normalize(e.NewValue));
            }, true);
        }

        /// <summary>
        ///     Switches the active theme directory and writes the choice to Ez config.
        /// </summary>
        public void SelectSet(string layoutSetName)

        {
            string normalized = EzLayoutSetNames.Normalize(layoutSetName);

            EzLayoutSetCandidates.EnsureListed(normalized);

            suppressBindableCallback = true;

            try

            {
                Store.UseSet(normalized);

                if (!this.layoutSetName.Value.Equals(normalized, StringComparison.Ordinal))

                    this.layoutSetName.Value = normalized;
            }

            finally

            {
                suppressBindableCallback = false;
            }
        }

        public event Action? SetChanged

        {
            add => Store.SetChanged += value;

            remove => Store.SetChanged -= value;
        }
    }
}
