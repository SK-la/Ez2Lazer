// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Screens.Footer;
using Realms;

namespace osu.Game.Screens.Ranking
{
    public partial class CollectionButton : ScreenFooterButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly Bindable<bool> isInAnyCollection;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private IDisposable? collectionSubscription;

        public CollectionButton(BeatmapInfo beatmapInfo)
        {
            this.beatmapInfo = beatmapInfo;
            isInAnyCollection = new Bindable<bool>(false);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Icon = FontAwesome.Solid.Book;
            AccentColour = colours.Lime1;
            Text = CommonStrings.Collections;
            TooltipText = CommonStrings.Collections;
            Action = this.ShowPopover;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            collectionSubscription = realmAccess.RegisterForNotifications(r => r.All<BeatmapCollection>(), collectionsChanged);

            isInAnyCollection.BindValueChanged(_ => updateState(), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            collectionSubscription?.Dispose();
        }

        private void collectionsChanged(IRealmCollection<BeatmapCollection> sender, ChangeSet? changes)
        {
            isInAnyCollection.Value = sender.AsEnumerable().Any(c => c.BeatmapMD5Hashes.Contains(beatmapInfo.MD5Hash));
        }

        /// <summary>
        /// 谱面属于任意收藏夹时让按钮保持高亮，等价于旧版绿色背景。
        /// </summary>
        private void updateState() => OverlayState.Value = isInAnyCollection.Value ? Visibility.Visible : Visibility.Hidden;

        public Popover GetPopover() => new CollectionPopover(beatmapInfo);
    }
}
