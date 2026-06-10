// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private const double dynamic_preview_duration = 10000;
        private const double dynamic_preview_repeat_delay = 500;

        private readonly EzSkinEditorSceneContext context;

        private readonly StopwatchClock previewClock = new StopwatchClock();
        private readonly FramedClock framedPreviewClock;

        private Container stageScaleContainer = null!;
        private Container stageViewport = null!;

        private DrawableRuleset? drawableRuleset;
        private IManiaStaticPreviewRenderer? maniaStaticRenderer;
        private CancellationTokenSource? loadCancellation;

        private IBeatmap? playableBeatmap;
        private RulesetInfo? rulesetInfo;
        private EzBeatmapPreviewMode previewMode;
        private double playbackStartTime;
        private double beatmapMinTime;
        private double beatmapMaxTime;
        private double nextDynamicLoopStartTime;

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

            InternalChild = stageViewport = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(10),
                Child = stageScaleContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
            };

            if (context.PreviewBeatmap == null || context.PreviewRuleset == null)
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
                return;
            }

            if (!EzSkinEditorPreviewModes.SupportsBeatmapPreview(context.PreviewRuleset))
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_RULESET_PREVIEW_NOT_SUPPORTED);
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
            double startTime;

            try
            {
                var ruleset = rulesetInfo!;
                beatmap = context.PreviewBeatmap!.GetPlayableBeatmap(ruleset);
                startTime = computeDefaultStartTime(beatmap, ruleset, beatmap.HitObjects.Count > 0 ? beatmap.HitObjects[0].StartTime : 0);
            }
            catch
            {
                stageScaleContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_LOAD_FAILED);
                return;
            }

            if (token.IsCancellationRequested)
                return;

            playableBeatmap = beatmap;
            playbackStartTime = startTime;
            beatmapMinTime = 0;
            beatmapMaxTime = Math.Max(beatmap.BeatmapInfo.Length, beatmapMinTime + 1);

            mountPreview(token);
            previewClock.Seek(playbackStartTime);

            if (previewMode == EzBeatmapPreviewMode.Dynamic)
            {
                nextDynamicLoopStartTime = Time.Current;
                previewClock.Start();
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
            var newDrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap);

            stageScaleContainer.RelativeSizeAxes = Axes.None;
            stageScaleContainer.Size = new Vector2(640, 480);

            newDrawableRuleset.Clock = framedPreviewClock;
            newDrawableRuleset.FrameStablePlayback = false;
            newDrawableRuleset.Playfield.DisplayJudgements.Value = false;

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

            if (!isDynamicPlayback || drawableRuleset == null)
                return;

            if (previewClock.IsRunning)
            {
                double elapsed = previewClock.CurrentTime - playbackStartTime;

                if (elapsed >= dynamic_preview_duration)
                {
                    previewClock.Stop();
                    previewClock.Seek(playbackStartTime);
                    nextDynamicLoopStartTime = Time.Current + dynamic_preview_repeat_delay;
                }
            }
            else if (Time.Current >= nextDynamicLoopStartTime)
            {
                previewClock.Seek(playbackStartTime);
                previewClock.Start();
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (drawableRuleset == null || maniaStaticRenderer != null)
                return;

            float viewportWidth = stageViewport.DrawWidth;
            float viewportHeight = stageViewport.DrawHeight;

            if (viewportWidth <= 1 || viewportHeight <= 1)
                return;

            float scale = Math.Min(viewportWidth / stageScaleContainer.Width, viewportHeight / stageScaleContainer.Height);
            stageScaleContainer.Scale = new Vector2(Math.Max(0.05f, scale));
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

            if (change.NewValue == EzBeatmapPreviewMode.Dynamic)
            {
                nextDynamicLoopStartTime = Time.Current;
                previewClock.Start();
            }
            else
            {
                nextDynamicLoopStartTime = double.PositiveInfinity;
                previewClock.Stop();
            }
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

        private static double computeDefaultStartTime(IBeatmap playableBeatmap, RulesetInfo ruleset, double fallback)
        {
            if (ruleset.OnlineID == 3)
            {
                int previewTime = playableBeatmap.Metadata.PreviewTime;
                if (previewTime > 0)
                    return previewTime;
            }
            else
            {
                double kiaiStart = getKiaiStartTime(playableBeatmap);
                if (!double.IsNaN(kiaiStart))
                    return kiaiStart;
            }

            return fallback;
        }

        private static double getKiaiStartTime(IBeatmap beatmap)
        {
            try
            {
                foreach (var effectPoint in beatmap.ControlPointInfo.EffectPoints)
                {
                    if (effectPoint.KiaiMode)
                        return effectPoint.Time;
                }
            }
            catch
            {
                // ignored
            }

            return double.NaN;
        }
    }
}
