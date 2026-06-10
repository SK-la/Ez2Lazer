// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// In-editor beatmap preview using the same renderers as <see cref="Overlays.EzBeatmapPreviewOverlay"/>.
    /// </summary>
    public partial class EzSkinEditorBeatmapPreviewHost : Container
    {
        private const float playback_controls_height = 48;

        private readonly EzSkinEditorSceneContext context;

        private readonly StopwatchClock previewClock = new StopwatchClock();
        private readonly FramedClock framedPreviewClock;

        private Container stageViewport = null!;
        private Container stageScaleContainer = null!;
        private EzSkinEditorPlaybackControls playbackControls = null!;

        private DrawableRuleset? drawableRuleset;
        private IManiaStaticPreviewRenderer? maniaStaticRenderer;
        private CancellationTokenSource? loadCancellation;

        private IBeatmap? playableBeatmap;
        private RulesetInfo? rulesetInfo;
        private EzBeatmapPreviewMode previewMode;
        private double beatmapMinTime;
        private double beatmapMaxTime;
        private double lastProgressDisplayTime = double.MinValue;

        private Bindable<EzBeatmapPreviewMode>? previewModeBindable;

        public EzSkinEditorBeatmapPreviewHost(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
            framedPreviewClock = new FramedClock(previewClock);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (context.PreviewState != null)
            {
                previewModeBindable = context.PreviewState.Mode.GetBoundCopy();
                previewModeBindable.BindValueChanged(onPreviewModeChanged, true);
            }

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 1),
                    new Dimension(GridSizeMode.Absolute, playback_controls_height),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        stageViewport = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(10),
                            Child = stageScaleContainer = new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                    },
                    new Drawable[]
                    {
                        playbackControls = new EzSkinEditorPlaybackControls
                        {
                            OnSeek = seekTo,
                            OnPlayStateChanged = setPlaying,
                        },
                    },
                },
            };

            if (context.PreviewBeatmap == null || context.PreviewRuleset == null)
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
                playbackControls.Alpha = 0;
                return;
            }

            if (!EzSkinEditorPreviewModes.SupportsBeatmapPreview(context.PreviewRuleset))
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_RULESET_PREVIEW_NOT_SUPPORTED);
                playbackControls.Alpha = 0;
                return;
            }

            rulesetInfo = context.PreviewRuleset;
            previewMode = EzSkinEditorPreviewModes.ValidateMode(context.PreviewMode, context.PreviewRuleset);
            beginLoad();
        }

        private void beginLoad()
        {
            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();
            var token = loadCancellation.Token;

            IBeatmap beatmap;

            try
            {
                var ruleset = rulesetInfo!;
                beatmap = context.PreviewBeatmap!.GetPlayableBeatmap(ruleset);
            }
            catch
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_LOAD_FAILED);
                playbackControls.Alpha = 0;
                return;
            }

            if (token.IsCancellationRequested)
                return;

            playableBeatmap = beatmap;
            beatmapMinTime = 0;
            beatmapMaxTime = Math.Max(beatmap.BeatmapInfo.Length, beatmap.HitObjects.Count > 0 ? beatmap.GetLastObjectTime() + 1000 : beatmapMinTime + 1);

            playbackControls.Alpha = 1;
            playbackControls.SetRange(beatmapMinTime, beatmapMaxTime);

            mountPreview(token);
            previewClock.Seek(beatmapMinTime);
            updateProgressDisplay(beatmapMinTime);

            if (isDynamicPlayback)
            {
                playbackControls.SetPlaying(true);
                previewClock.Start();
            }
            else
            {
                playbackControls.SetPlaying(false);
                previewClock.Stop();
            }
        }

        private void mountPreview(CancellationToken token)
        {
            disposePreviewResources();

            if (playableBeatmap == null || rulesetInfo == null)
                return;

            if (EzSkinEditorPreviewModes.IsManiaRuleset(rulesetInfo)
                && previewMode is EzBeatmapPreviewMode.StaticFullMap or EzBeatmapPreviewMode.StaticScroll)
            {
                setupManiaStaticPreview(playableBeatmap);
                return;
            }

            var ruleset = rulesetInfo.CreateInstance();
            IReadOnlyList<Mod>? autoplayMods = null;

            if (ruleset.GetAutoplayMod() is Mod autoplayMod)
                autoplayMods = new[] { autoplayMod };

            var newDrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, autoplayMods);

            stageScaleContainer.RelativeSizeAxes = Axes.None;
            stageScaleContainer.Size = new Vector2(640, 480);

            newDrawableRuleset.Clock = framedPreviewClock;
            newDrawableRuleset.FrameStablePlayback = false;
            newDrawableRuleset.Playfield.DisplayJudgements.Value = true;

            var previewRoot = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, context.EditorSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new NonHandleContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = newDrawableRuleset,
                },
            };

            LoadComponentAsync(previewRoot, loaded =>
            {
                Schedule(() =>
                {
                    if (token.IsCancellationRequested || !IsLoaded || IsDisposed)
                    {
                        loaded.Dispose();
                        return;
                    }

                    stageScaleContainer.Child = loaded;
                    drawableRuleset = newDrawableRuleset;
                    EzSkinEditorRulesetPreviewBootstrap.ApplyAutoplayReplay(newDrawableRuleset, playableBeatmap!);
                    maniaStaticRenderer = null;
                });
            }, token);
        }

        private void setupManiaStaticPreview(IBeatmap beatmap)
        {
            ManiaPreviewData data = ManiaPreviewGeometryBuilder.Build(beatmap);

            var renderer = previewMode switch
            {
                EzBeatmapPreviewMode.StaticFullMap => (IManiaStaticPreviewRenderer)new StaticFullMapPreviewRenderer(),
                EzBeatmapPreviewMode.StaticScroll => new StaticScrollPreviewRenderer(),
                _ => null,
            };

            if (renderer == null)
                return;

            renderer.SetData(data);
            renderer.SetCurrentTime(previewClock.CurrentTime);

            stageScaleContainer.RelativeSizeAxes = Axes.Both;
            stageScaleContainer.Scale = Vector2.One;
            stageScaleContainer.Size = Vector2.One;
            stageScaleContainer.Child = (Drawable)renderer;

            maniaStaticRenderer = renderer;
            drawableRuleset = null;
        }

        private bool isDynamicPlayback =>
            (previewModeBindable?.Value ?? previewMode) == EzBeatmapPreviewMode.Dynamic;

        protected override void Update()
        {
            base.Update();

            if (isDynamicPlayback && drawableRuleset != null && previewClock.IsRunning)
            {
                if (previewClock.CurrentTime >= beatmapMaxTime)
                    previewClock.Seek(beatmapMinTime);

                if (previewClock.CurrentTime - lastProgressDisplayTime >= 16)
                {
                    updateProgressDisplay(previewClock.CurrentTime);
                    lastProgressDisplayTime = previewClock.CurrentTime;
                }
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (drawableRuleset == null && maniaStaticRenderer == null)
                return;

            float viewportWidth = stageViewport.DrawWidth;
            float viewportHeight = stageViewport.DrawHeight;

            if (viewportWidth <= 1 || viewportHeight <= 1)
                return;

            if (maniaStaticRenderer != null)
                return;

            float scale = Math.Min(viewportWidth / stageScaleContainer.Width, viewportHeight / stageScaleContainer.Height);
            stageScaleContainer.Scale = new Vector2(Math.Max(0.05f, scale));
        }

        private void seekTo(double time)
        {
            double clamped = Math.Clamp(time, beatmapMinTime, beatmapMaxTime);
            previewClock.Seek(clamped);

            if (maniaStaticRenderer != null)
            {
                maniaStaticRenderer.SetCurrentTime(clamped);

                if (maniaStaticRenderer is StaticScrollPreviewRenderer scrollRenderer)
                {
                    float progress = beatmapMaxTime > beatmapMinTime
                        ? (float)((clamped - beatmapMinTime) / (beatmapMaxTime - beatmapMinTime))
                        : 0;
                    scrollRenderer.SetScrollProgress(progress);
                }
            }

            updateProgressDisplay(clamped);
        }

        private void setPlaying(bool playing)
        {
            if (playing)
                previewClock.Start();
            else
                previewClock.Stop();

            playbackControls.SetPlaying(playing);
        }

        private void updateProgressDisplay(double time)
        {
            playbackControls.SetCurrentTime(time);
        }

        private void disposePreviewResources()
        {
            drawableRuleset = null;
            maniaStaticRenderer = null;

            if (stageScaleContainer.Count > 0)
                stageScaleContainer.Clear(true);
        }

        private void onPreviewModeChanged(ValueChangedEvent<EzBeatmapPreviewMode> change)
        {
            previewMode = change.NewValue;

            if (playableBeatmap == null)
                return;

            if (change.NewValue == EzBeatmapPreviewMode.Dynamic)
            {
                playbackControls.SetPlaying(true);
                previewClock.Start();
            }
            else
            {
                playbackControls.SetPlaying(false);
                previewClock.Stop();
            }

            beginLoad();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                previewModeBindable?.UnbindAll();
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;
                drawableRuleset = null;
                maniaStaticRenderer = null;
            }

            base.Dispose(isDisposing);
        }

        private static OsuSpriteText createPlaceholder(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };
    }
}
