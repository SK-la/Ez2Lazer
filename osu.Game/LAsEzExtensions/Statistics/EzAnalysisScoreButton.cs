using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osuTK;
using Realms;

namespace osu.Game.LAsEzExtensions.Statistics
{
    public partial class EzAnalysisScoreButton : GrayButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly Bindable<bool> isInAnyCollection;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        private IDisposable? collectionSubscription;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzAnalysisScoreButton(BeatmapInfo beatmapInfo)
            : base(FontAwesome.Solid.Book)
        {
            this.beatmapInfo = beatmapInfo;
            isInAnyCollection = new Bindable<bool>(false);

            Size = new Vector2(75, 30);

            TooltipText = "Other MUG Determinator";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = ShowPopover;
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

        private void updateState()
        {
            Background.FadeColour(isInAnyCollection.Value ? colours.Green : colours.Gray4, 500, Easing.InOutExpo);
        }

        public Popover GetPopover() => new EzAnalysisOptionsPopover(beatmapInfo);

        public void ShowPopover()
        {
            var lAsAnalysisOptionsPopover = new EzAnalysisOptionsPopover(beatmapInfo);
            if (lAsAnalysisOptionsPopover == null) throw new ArgumentNullException(nameof(lAsAnalysisOptionsPopover));
        }
    }
}
