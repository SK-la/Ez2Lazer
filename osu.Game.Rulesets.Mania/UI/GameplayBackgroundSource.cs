// filepath: osu.Game/Screens/Play/GameplayBackgroundSource.cs
// Implementation of IGameplayBackgroundSource combining beatmap background + storyboard overlay layers.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;

namespace osu.Game.Rulesets.Mania.UI
{
    public class GameplayBackgroundSource : IGameplayBackgroundSource
    {
        private readonly Player player;

        public GameplayBackgroundSource(Player player)
        {
            this.player = player;
        }

        public Drawable CreateCompositeProxy()
        {
            var composite = new Container
            {
                RelativeSizeAxes = Axes.Both
            };

            try
            {
                player.ApplyToBackground(b =>
                {
                    var bgProxy = b.CreateBackdropProxy();
                    if (bgProxy != null)
                        composite.Add(bgProxy);
                });
            }
            catch
            {
                // ignore if background not available.
            }

            if (player.DimmableStoryboard != null)
            {
                composite.Add(player.DimmableStoryboard.CreateProxy());
            }

            return composite;
        }

        public Drawable? CreateBackgroundOnlyProxy()
        {
            Drawable? result = null;

            try
            {
                player.ApplyToBackground(b =>
                {
                    result = b.CreateBackdropProxy();
                });
            }
            catch { }

            return result;
        }

        public Drawable? CreateBackgroundWithVideoProxy()
        {
            // Use real background (non-proxy) to avoid dim / black issues from proxy state.
            var container = new Container { RelativeSizeAxes = Axes.Both };

            try
            {
                var bg = CreateStandaloneBackground();
                if (bg != null)
                    container.Add(bg);
            }
            catch { }

            try
            {
                var storyboard = player.GameplayState?.Storyboard;

                if (storyboard != null)
                {
                    foreach (var layer in storyboard.Layers)
                    {
                        if (layer is StoryboardVideoLayer videoLayer)
                        {
                            var drawableLayer = videoLayer.CreateDrawable();

                            if (drawableLayer != null)
                            {
                                drawableLayer.RelativeSizeAxes = Axes.Both;
                                container.Add(drawableLayer);
                            }

                            break;
                        }
                    }
                }
            }
            catch { }

            return container;
        }

        public Drawable CreateStandaloneBackground()
        {
            // Prefer the working beatmap from GameplayState if available.
            var working = player.Beatmap.Value; // WorkingBeatmap
            var background = new BeatmapBackground(working)
            {
                RelativeSizeAxes = Axes.Both
            };
            return background;
        }
    }
}
