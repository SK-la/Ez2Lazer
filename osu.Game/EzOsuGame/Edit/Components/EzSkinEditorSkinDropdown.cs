// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorSkinDropdown : OsuDropdown<Live<SkinInfo>>
    {
        public const float DEFAULT_WIDTH = 200;

        private readonly List<Live<SkinInfo>> dropdownItems = new List<Live<SkinInfo>>();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IDisposable? realmSubscription;

        public EzSkinEditorSkinDropdown()
        {
            RelativeSizeAxes = Axes.None;
            Width = DEFAULT_WIDTH;
            AlwaysShowSearchBar = true;
            AllowNonContiguousMatching = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Current = skins.CurrentSkinInfo;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>()
                                                                         .Where(s => !s.DeletePending)
                                                                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            Current.BindValueChanged(skin =>
            {
                if (skin.NewValue.ID == SkinInfo.RANDOM_SKIN)
                {
                    skins.CurrentSkinInfo.Value = skin.OldValue;
                    skins.SelectRandomSkin();
                }
            });

            skins.ScriptedSkinsCatalogUpdated += refreshSkinsList;
            refreshSkinsList();
        }

        protected override LocalisableString GenerateItemText(Live<SkinInfo> item) => item.ToString() ?? "";

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet? changes)
        {
            if (!sender.Any())
                return;

            refreshSkinsList();
        }

        private void refreshSkinsList()
        {
            dropdownItems.Clear();
            dropdownItems.AddRange(skins.GetAllUsableSkins());
            Schedule(() => Items = dropdownItems);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                skins.ScriptedSkinsCatalogUpdated -= refreshSkinsList;
                realmSubscription?.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
