// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Online;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Ranking
{
    public partial class FavouriteButton : ScreenFooterButton
    {
        public readonly BeatmapSetInfo BeatmapSetInfo;
        private APIBeatmapSet? beatmapSet;
        private readonly Bindable<BeatmapSetFavouriteState> current;

        private PostBeatmapFavouriteRequest? favouriteRequest;
        private LoadingLayer loading = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public FavouriteButton(BeatmapSetInfo beatmapSetInfo)
        {
            BeatmapSetInfo = beatmapSetInfo;
            current = new BindableWithCurrent<BeatmapSetFavouriteState>(new BeatmapSetFavouriteState(false, 0));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(loading = new LoadingLayer(true, false));

            Action = toggleFavouriteStatus;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(_ => updateState(), true);

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => updateUser(), true);

            // 离线、连接中或本地账号下 LocalUser 可能不变，状态变化也要重新判定。
            api.State.BindValueChanged(_ => updateUser(), true);
        }

        private void getBeatmapSet()
        {
            GetBeatmapSetRequest beatmapSetRequest = new GetBeatmapSetRequest(BeatmapSetInfo.OnlineID);

            loading.Show();
            beatmapSetRequest.Success += beatmapSet =>
            {
                this.beatmapSet = beatmapSet;
                current.Value = new BeatmapSetFavouriteState(this.beatmapSet.HasFavourited, this.beatmapSet.FavouriteCount);

                loading.Hide();
                Enabled.Value = true;
            };
            beatmapSetRequest.Failure += e =>
            {
                if (e is not LocalOnlyUnavailableException)
                    Logger.Log($"Favourite button failed to fetch beatmap info: {e}", LoggingTarget.Network);

                Schedule(() =>
                {
                    loading.Hide();
                    Enabled.Value = false;
                    TooltipText = "this beatmap cannot be favourited";
                });
            };
            api.Queue(beatmapSetRequest);
        }

        private void toggleFavouriteStatus()
        {
            if (beatmapSet == null)
                return;

            Enabled.Value = false;
            loading.Show();

            var actionType = current.Value.Favourited ? BeatmapFavouriteAction.UnFavourite : BeatmapFavouriteAction.Favourite;

            favouriteRequest?.Cancel();
            favouriteRequest = new PostBeatmapFavouriteRequest(beatmapSet.OnlineID, actionType);
            favouriteRequest.Success += () =>
            {
                bool favourited = actionType == BeatmapFavouriteAction.Favourite;

                current.Value = new BeatmapSetFavouriteState(favourited, current.Value.FavouriteCount + (favourited ? 1 : -1));

                Enabled.Value = true;
                loading.Hide();
                api.LocalUserState.UpdateFavouriteBeatmapSets();
            };
            favouriteRequest.Failure += e =>
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = e.Message,
                    Icon = FontAwesome.Solid.Times,
                });

                Schedule(() =>
                {
                    Enabled.Value = true;
                    loading.Hide();
                });
            };

            api.Queue(favouriteRequest);
        }

        private void updateUser()
        {
            if (api.IsLocalOnly)
            {
                Enabled.Value = false;
                current.Value = new BeatmapSetFavouriteState(false, 0);
                updateState();
                TooltipText = "this beatmap cannot be favourited";
                return;
            }

            // 状态非在线（登出、连接中）时 LocalUser 可能还是旧值，不应据此拉取收藏状态。
            if (api.State.Value == APIState.Online && !(localUser.Value is GuestUser) && BeatmapSetInfo.OnlineID > 0)
                getBeatmapSet();
            else
            {
                Enabled.Value = false;
                current.Value = new BeatmapSetFavouriteState(false, 0);
                updateState();
                TooltipText = BeatmapsetsStrings.ShowDetailsFavouriteLogin;
            }
        }

        private void updateState()
        {
            if (current.Value.Favourited)
            {
                AccentColour = colours.Lime1;
                OverlayState.Value = Visibility.Visible;
                Icon = FontAwesome.Solid.Heart;
                Text = FavouriteStrings.UNFAVOURITE;
                TooltipText = BeatmapsetsStrings.ShowDetailsUnfavourite;
            }
            else
            {
                AccentColour = colours.Pink1;
                OverlayState.Value = Visibility.Hidden;
                Icon = FontAwesome.Regular.Heart;
                Text = FavouriteStrings.FAVOURITE;
                TooltipText = BeatmapsetsStrings.ShowDetailsFavourite;
            }
        }

        private static class FavouriteStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString FAVOURITE = new EzLocalizationManager.EzLocalisableString(
                "收藏", "Favourite");

            internal static readonly EzLocalizationManager.EzLocalisableString UNFAVOURITE = new EzLocalizationManager.EzLocalisableString(
                "取消收藏", "Unfavourite");
        }
    }
}
