// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Loader screen that prepares and starts BMS gameplay using Mania's standard Player.
    /// Converts BMS beatmap to Mania format and uses osu!'s standard Player flow.
    /// </summary>
    public partial class BMSPlayerLoader : OsuScreen
    {
        protected override bool InitialBackButtonVisibility => false;

        private readonly BMSWorkingBeatmap workingBeatmap;
        private LoadingSpinner loadingSpinner = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText titleText = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public BMSPlayerLoader(BMSWorkingBeatmap workingBeatmap)
        {
            this.workingBeatmap = workingBeatmap;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(0, 20),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.Music,
                            Size = new Vector2(64),
                            Colour = colours.Yellow,
                        },
                        titleText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "加载中...",
                            Font = OsuFont.GetFont(size: 32, weight: FontWeight.Bold),
                        },
                        statusText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "正在解析 BMS 文件...",
                            Font = OsuFont.GetFont(size: 18),
                            Colour = colours.Yellow,
                        },
                        loadingSpinner = new LoadingSpinner
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(50),
                        },
                    },
                },
            };

            loadingSpinner.Show();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Start loading after a brief delay
            Scheduler.AddDelayed(LoadBeatmap, 100);
        }

        private void LoadBeatmap()
        {
            try
            {
                statusText.Text = "正在解析谱面...";

                // Force load the beatmap
                var beatmap = workingBeatmap.Beatmap;

                if (beatmap == null || beatmap.HitObjects.Count == 0)
                {
                    statusText.Text = "错误: 谱面加载失败或没有音符";
                    loadingSpinner.Hide();
                    Scheduler.AddDelayed(() => this.Exit(), 2000);
                    return;
                }

                // Update title display
                var metadata = workingBeatmap.BeatmapInfo.Metadata;
                titleText.Text = string.IsNullOrEmpty(metadata.Title) ? "BMS" : metadata.Title;
                statusText.Text = $"加载完成! {beatmap.HitObjects.Count} 个音符";

                // Small delay then push to player
                Scheduler.AddDelayed(() =>
                {
                    loadingSpinner.Hide();
                    PushPlayer();
                }, 500);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "Failed to load BMS beatmap for play");
                statusText.Text = $"加载失败: {ex.Message}";
                loadingSpinner.Hide();
                Scheduler.AddDelayed(() => this.Exit(), 3000);
            }
        }

        private void PushPlayer()
        {
            try
            {
                // Convert BMS beatmap to Mania beatmap and create a working beatmap wrapper
                var maniaWorkingBeatmap = new ManiaConvertedWorkingBeatmap(workingBeatmap, audioManager);
                var maniaRuleset = new ManiaRuleset();

                // Set the global beatmap and ruleset to the converted Mania beatmap
                Beatmap.Value = maniaWorkingBeatmap;
                Ruleset.Value = maniaRuleset.RulesetInfo;

                // Use osu!'s standard PlayerLoader which will create SoloPlayer
                var playerLoader = new PlayerLoader(() => new SoloPlayer());
                this.Push(playerLoader);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "Failed to push BMS player");
                statusText.Text = $"启动游戏失败: {ex.Message}";
                Scheduler.AddDelayed(() => this.Exit(), 3000);
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(300);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(200);
            return base.OnExiting(e);
        }
    }
}
