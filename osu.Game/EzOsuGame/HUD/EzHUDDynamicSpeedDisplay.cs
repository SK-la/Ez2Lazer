// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Mods.CommunityMod;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzHUDDynamicSpeedDisplay : CompositeDrawable, ISerialisableDrawable
    {
        private const double speed_step = 0.01;
        private const float pixels_per_speed_step = 2f;
        private const float line_thickness = 2f;
        private const float text_line_spacing = 8f;
        private const float blink_dot_radius = 3f;
        private const double change_threshold = 0.001;
        private const float blink_min_alpha = 0.15f;
        private const float blink_max_alpha = 0.45f;
        private const double blink_frequency = 6.0;

        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DYNAMIC_SPEED_SHOW_LINE_LABEL), nameof(EzHUDStrings.DYNAMIC_SPEED_SHOW_LINE_DESCRIPTION))]
        public BindableBool ShowSpeedLine { get; } = new BindableBool(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DYNAMIC_SPEED_LINE_WIDTH_LABEL), nameof(EzHUDStrings.DYNAMIC_SPEED_LINE_WIDTH_DESCRIPTION))]
        public BindableNumber<float> LineWidth { get; } = new BindableNumber<float>(200)
        {
            MinValue = 50,
            MaxValue = 600,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DYNAMIC_SPEED_LINE_HEIGHT_LABEL), nameof(EzHUDStrings.DYNAMIC_SPEED_LINE_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> LineHeight { get; } = new BindableNumber<float>(48)
        {
            MinValue = 24,
            MaxValue = 200,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.DYNAMIC_SPEED_ENDPOINT_BLINK_LABEL), nameof(EzHUDStrings.DYNAMIC_SPEED_ENDPOINT_BLINK_DESCRIPTION))]
        public BindableBool ShowEndpointBlink { get; } = new BindableBool(true);

        private readonly BindableDouble internalSpeed = new BindableDouble(1);
        private readonly IBindable<double> speedSource;
        private readonly bool usesExternalSpeedSource;
        private readonly IBindable<bool>? showTextSource;
        private readonly BindableBool? lineVisibilitySource;
        private readonly bool defaultShowText;

        private GameplayClockContainer? gameplayClockContainer;
        private Player? player;
        private double? entryReferenceSpeed;
        private double lastMapStartTime = double.NaN;

        private FillFlowContainer? layout;
        private readonly OsuSpriteText speedText;
        private Container? lineContainer;
        private Path? speedPath;
        private Circle? endpointBlink;
        private SpeedLineSampler? lineSampler;
        private bool lineComponentsLoaded;

        public EzHUDDynamicSpeedDisplay()
        {
            speedSource = internalSpeed;
            usesExternalSpeedSource = false;
            defaultShowText = true;

            AutoSizeAxes = Axes.X;
            Masking = false;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Position = new Vector2(50, -50);

            InternalChild = speedText = new OsuSpriteText
            {
                Font = OsuFont.Default.With(size: 24),
                Colour = Colour4.White,
                Text = "1.00x",
            };
        }

        public EzHUDDynamicSpeedDisplay(IBindable<double> speedSource, IBindable<bool>? textVisibility = null, BindableBool? lineVisibility = null)
        {
            this.speedSource = speedSource;
            usesExternalSpeedSource = true;
            showTextSource = textVisibility;
            lineVisibilitySource = lineVisibility;
            defaultShowText = textVisibility?.Value ?? true;

            AutoSizeAxes = Axes.X;
            Masking = false;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Position = new Vector2(50, -50);

            InternalChild = speedText = new OsuSpriteText
            {
                Font = OsuFont.Default.With(size: 24),
                Colour = Colour4.White,
                Text = $"{speedSource.Value:0.00}x",
            };
        }

        private void createLineComponents()
        {
            if (lineComponentsLoaded)
                return;

            RemoveInternal(speedText, false);

            endpointBlink = new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(blink_dot_radius * 2),
                Colour = Colour4.White,
                Alpha = 0,
                Blending = BlendingParameters.Additive,
            };

            speedPath = new Path();
            speedPath.AutoSizeAxes = Axes.None;
            speedPath.Anchor = Anchor.CentreLeft;
            speedPath.Origin = Anchor.CentreLeft;
            speedPath.PathRadius = line_thickness * 0.5f;
            speedPath.Colour = Colour4.White;

            lineContainer = new Container
            {
                Masking = false,
                RelativeSizeAxes = Axes.None,
                Width = LineWidth.Value,
                Height = LineHeight.Value,
                Children = new Drawable[] { endpointBlink },
            };

            lineContainer.Add(speedPath);

            lineSampler = new SpeedLineSampler(speedSource, speedPath, endpointBlink);
            lineSampler.SpeedChangingChanged += _ => updateEndpointBlink();

            layout = new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.None,
                Masking = false,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(text_line_spacing, 0),
            };

            speedText.Anchor = Anchor.CentreLeft;
            speedText.Origin = Anchor.CentreLeft;

            layout.Add(speedText);
            layout.Add(lineContainer);
            AddInternal(layout);

            lineComponentsLoaded = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            createLineComponents();

            gameplayClockContainer = this.FindClosestParent<GameplayClockContainer>();
            player = this.FindClosestParent<Player>();

            speedSource.BindValueChanged(_ => updateSpeedText(), true);
            showTextSource?.BindValueChanged(_ => updateVisibility(), true);

            if (lineVisibilitySource != null)
            {
                ShowSpeedLine.Value = lineVisibilitySource.Value;
                ShowSpeedLine.BindTo(lineVisibilitySource);
            }

            ShowSpeedLine.BindValueChanged(_ => updateVisibility(), true);
            LineWidth.BindValueChanged(_ => updateLayout(), true);
            LineHeight.BindValueChanged(_ => updateLayout(), true);
            ShowEndpointBlink.BindValueChanged(_ => updateEndpointBlink(), true);

            if (speedSource is BindableNumber<double> speedBindable)
            {
                speedBindable.MinValueChanged += _ => updateSpeedMapping();
                speedBindable.MaxValueChanged += _ => updateSpeedMapping();
            }

            ensureEntryReferenceSpeedCaptured();
            updateSpeedMapping();
            updateVisibility();
        }

        protected override void Update()
        {
            base.Update();

            if (!usesExternalSpeedSource)
            {
                gameplayClockContainer ??= this.FindClosestParent<GameplayClockContainer>();
                player ??= this.FindClosestParent<Player>();
                updateSpeedFromGameplay();
            }
            else
            {
                gameplayClockContainer ??= this.FindClosestParent<GameplayClockContainer>();
                player ??= this.FindClosestParent<Player>();
                ensureEntryReferenceSpeedCaptured();
            }

            if (!lineComponentsLoaded)
                return;

            updateLineGraphTiming();

            if (shouldSampleLine())
                lineSampler!.SampleCurrentSpeed(lineContainer!.DrawWidth, lineContainer.DrawHeight);

            updateEndpointBlink();
        }

        private void updateLineGraphTiming()
        {
            if (!lineComponentsLoaded)
                return;

            gameplayClockContainer ??= this.FindClosestParent<GameplayClockContainer>();
            player ??= this.FindClosestParent<Player>();

            if (gameplayClockContainer == null)
                return;

            double startTime = gameplayClockContainer.StartTime;
            double endTime = player?.GameplayState?.Beatmap.GetLastObjectTime() ?? startTime + 1;

            if (endTime <= startTime)
                endTime = startTime + 1;

            if (!double.IsNaN(lastMapStartTime) && Math.Abs(lastMapStartTime - startTime) > double.Epsilon)
                entryReferenceSpeed = null;

            lastMapStartTime = startTime;

            lineSampler!.SetMapTiming(startTime, endTime);
            lineSampler.SetCurrentTime(gameplayClockContainer.CurrentTime);
        }

        private bool shouldSampleLine()
        {
            if (gameplayClockContainer == null)
                return false;

            return !gameplayClockContainer.IsPaused.Value;
        }

        private void updateLayout()
        {
            if (!lineComponentsLoaded)
                return;

            float areaHeight = LineHeight.Value;
            layout!.Height = areaHeight;
            Height = areaHeight;

            bool textVisible = showTextSource?.Value ?? defaultShowText;

            if (textVisible)
                speedText.Y = lineSampler!.MapSpeedToY(getReferenceSpeedForAlignment(), areaHeight);

            lineContainer!.Width = ShowSpeedLine.Value ? LineWidth.Value : 0;
            lineContainer.Height = areaHeight;
            speedPath!.Size = new Vector2(lineContainer.Width, areaHeight);
            lineSampler!.RebuildPath(lineContainer.DrawWidth, lineContainer.DrawHeight);
        }

        private double getReferenceSpeedForAlignment() => entryReferenceSpeed ?? 1.0;

        private void updateSpeedMapping()
        {
            ensureEntryReferenceSpeedCaptured();

            if (!lineComponentsLoaded)
                return;

            var sampler = lineSampler!;

            if (tryResolveEdgeRange(out double minSpeed, out double maxSpeed))
                sampler.SetEdgeRange(minSpeed, maxSpeed);
            else
                sampler.SetCenterScale(getReferenceSpeedForAlignment(), speed_step, pixels_per_speed_step);

            updateLayout();
        }

        private bool tryResolveEdgeRange(out double minSpeed, out double maxSpeed)
        {
            var dynamicMod = player?.GameplayState?.Mods.OfType<ILinkedDynamicSpeedHUD>().FirstOrDefault();

            if (dynamicMod != null && tryGetModDisplayRange(dynamicMod, out minSpeed, out maxSpeed))
                return true;

            if (usesExternalSpeedSource && speedSource is BindableNumber<double> speedBindable)
            {
                minSpeed = speedBindable.MinValue;
                maxSpeed = speedBindable.MaxValue;
                return maxSpeed > minSpeed;
            }

            minSpeed = 0;
            maxSpeed = 0;
            return false;
        }

        private static bool tryGetModDisplayRange(ILinkedDynamicSpeedHUD mod, out double minSpeed, out double maxSpeed)
        {
            switch (mod)
            {
                case ModNiceBPM niceBpm:
                    minSpeed = niceBpm.MinAllowableRate.Value;
                    maxSpeed = niceBpm.MaxAllowableRate.Value;
                    return maxSpeed > minSpeed;

                case ModAccuracyAdaptive accuracyAdaptive:
                    minSpeed = accuracyAdaptive.MinAllowableRate.Value;
                    maxSpeed = accuracyAdaptive.MaxAllowableRate.Value;
                    return maxSpeed > minSpeed;

                case ModHealthAdaptive healthAdaptive:
                    minSpeed = healthAdaptive.MinAllowableRate.Value;
                    maxSpeed = healthAdaptive.MaxAllowableRate.Value;
                    return maxSpeed > minSpeed;

                default:
                    minSpeed = mod.SpeedChange.MinValue;
                    maxSpeed = mod.SpeedChange.MaxValue;
                    return maxSpeed > minSpeed;
            }
        }

        private void ensureEntryReferenceSpeedCaptured()
        {
            if (entryReferenceSpeed.HasValue)
                return;

            if (usesExternalSpeedSource)
            {
                entryReferenceSpeed = speedSource.Value;
                return;
            }

            var dynamicMod = player?.GameplayState?.Mods.OfType<ILinkedDynamicSpeedHUD>().FirstOrDefault();

            if (dynamicMod != null)
            {
                entryReferenceSpeed = dynamicMod.SpeedChange.Value;
                return;
            }

            if (gameplayClockContainer != null)
                entryReferenceSpeed = gameplayClockContainer.GetTrueGameplayRate();
            else
                entryReferenceSpeed = 1.0;
        }

        private void updateSpeedFromGameplay()
        {
            var dynamicMod = player?.GameplayState?.Mods.OfType<ILinkedDynamicSpeedHUD>().FirstOrDefault();

            if (dynamicMod != null)
            {
                internalSpeed.Value = dynamicMod.SpeedChange.Value;
                updateSpeedMapping();
                return;
            }

            if (gameplayClockContainer != null)
                internalSpeed.Value = gameplayClockContainer.GetTrueGameplayRate();

            updateSpeedMapping();
        }

        private void updateSpeedText()
        {
            speedText.Text = $"{speedSource.Value:0.00}x";
            updateLayout();
        }

        private void updateVisibility()
        {
            bool textVisible = showTextSource?.Value ?? defaultShowText;

            speedText.Alpha = textVisible ? 1 : 0;

            if (lineComponentsLoaded)
            {
                lineContainer!.Alpha = ShowSpeedLine.Value ? 1 : 0;
                updateLayout();
                updateEndpointBlink();
            }
        }

        private void updateEndpointBlink()
        {
            if (!lineComponentsLoaded)
                return;

            lineSampler!.UpdateEndpointBlink(
                ShowSpeedLine.Value && ShowEndpointBlink.Value,
                Clock.CurrentTime);
        }

        private sealed class SpeedLineSampler
        {
            private enum SpeedAxisMapping
            {
                EdgeRange,
                CenterScale,
            }

            private const double sample_interval_ms = 10;
            private const int max_samples_per_update = 50;
            private const float pixels_per_sample = 2f;

            private readonly struct SpeedSample
            {
                public readonly double Time;
                public readonly double Speed;

                public SpeedSample(double time, double speed)
                {
                    Time = time;
                    Speed = speed;
                }
            }

            public event Action<bool>? SpeedChangingChanged;

            public bool IsSpeedChanging { get; private set; }

            private SpeedAxisMapping mapping = SpeedAxisMapping.CenterScale;
            private double minSpeed;
            private double maxSpeed;
            private double referenceSpeed = 1;
            private double speedStep = speed_step;
            private float pixelsPerSpeedStep = pixels_per_speed_step;

            private readonly IBindable<double> speedSource;
            private readonly Path path;
            private readonly Circle endpointBlink;
            private readonly List<SpeedSample> samples = new List<SpeedSample>();
            private double mapStartTime;
            private double mapEndTime = 1;
            private double currentTime;
            private double lastRecordedTime;
            private double lastSampledSpeed;
            private bool hasRecordedTime;

            public SpeedLineSampler(IBindable<double> speedSource, Path path, Circle endpointBlink)
            {
                this.speedSource = speedSource;
                this.path = path;
                this.endpointBlink = endpointBlink;
            }

            public void UpdateEndpointBlink(bool enabled, double clockTime)
            {
                if (!enabled || !IsSpeedChanging)
                {
                    endpointBlink.Alpha = 0;
                    return;
                }

                float pulse = (float)((Math.Sin(clockTime * blink_frequency) + 1) * 0.5);
                endpointBlink.Alpha = blink_min_alpha + pulse * (blink_max_alpha - blink_min_alpha);
            }

            public void SetEdgeRange(double min, double max)
            {
                mapping = SpeedAxisMapping.EdgeRange;
                minSpeed = min;
                maxSpeed = max;
            }

            public void SetCenterScale(double reference, double step, float pixelsPerStep)
            {
                mapping = SpeedAxisMapping.CenterScale;
                referenceSpeed = reference;
                speedStep = step;
                pixelsPerSpeedStep = pixelsPerStep;
            }

            public float MapSpeedToY(double speed, float areaHeight)
            {
                if (areaHeight <= 0)
                    return 0;

                switch (mapping)
                {
                    case SpeedAxisMapping.EdgeRange:
                    {
                        double span = maxSpeed - minSpeed;

                        if (span <= 0)
                            return areaHeight * 0.5f;

                        double normalised = (speed - minSpeed) / span;
                        return (float)(areaHeight * (1 - normalised));
                    }

                    default:
                    {
                        float centerY = areaHeight * 0.5f;
                        return centerY - (float)((speed - referenceSpeed) / speedStep * pixelsPerSpeedStep);
                    }
                }
            }

            public void SetMapTiming(double startTime, double endTime)
            {
                if (Math.Abs(mapStartTime - startTime) < double.Epsilon && Math.Abs(mapEndTime - endTime) < double.Epsilon)
                    return;

                mapStartTime = startTime;
                mapEndTime = endTime;
                resetSamples();
            }

            public void SetCurrentTime(double time) => currentTime = time;

            public void SampleCurrentSpeed(float drawWidth, float drawHeight)
            {
                if (mapDuration <= 0 || drawWidth <= 0 || drawHeight <= 0)
                    return;

                double currentSpeed = speedSource.Value;

                if (currentTime < lastRecordedTime - change_threshold)
                    truncateSamplesAfter(currentTime, drawWidth, drawHeight);

                if (!hasRecordedTime)
                {
                    lastRecordedTime = mapStartTime - sample_interval_ms;
                    hasRecordedTime = true;
                }

                bool added = false;
                int addedThisUpdate = 0;

                while (lastRecordedTime + sample_interval_ms <= currentTime && addedThisUpdate < max_samples_per_update)
                {
                    lastRecordedTime += sample_interval_ms;
                    samples.Add(new SpeedSample(lastRecordedTime, currentSpeed));
                    added = true;
                    addedThisUpdate++;
                }

                bool changing = Math.Abs(currentSpeed - lastSampledSpeed) > change_threshold;

                if (changing != IsSpeedChanging)
                {
                    IsSpeedChanging = changing;
                    SpeedChangingChanged?.Invoke(changing);
                }

                lastSampledSpeed = currentSpeed;

                if (!added)
                    return;

                RebuildPath(drawWidth, drawHeight);
                endpointBlink.Position = computeEndpointPosition(drawWidth, drawHeight);
            }

            public void RebuildPath(float drawWidth, float drawHeight)
            {
                if (drawWidth <= 0 || drawHeight <= 0 || mapDuration <= 0)
                {
                    path.Vertices = Array.Empty<Vector2>();
                    return;
                }

                if (mapping == SpeedAxisMapping.EdgeRange && maxSpeed <= minSpeed)
                {
                    path.Vertices = Array.Empty<Vector2>();
                    return;
                }

                if (samples.Count == 0 || currentTime < mapStartTime)
                {
                    path.Vertices = Array.Empty<Vector2>();
                    return;
                }

                int maxVertices = Math.Max(2, (int)(drawWidth / pixels_per_sample));
                var vertices = new List<Vector2>(maxVertices);

                for (int i = 0; i < maxVertices; i++)
                {
                    float x = drawWidth * i / (maxVertices - 1f);
                    double targetTime = mapStartTime + x / drawWidth * mapDuration;

                    if (targetTime > currentTime + change_threshold)
                        break;

                    var sample = findSampleAtOrBefore(targetTime);
                    vertices.Add(new Vector2(x, mapSpeedToY(sample.Speed, drawHeight)));
                }

                path.Vertices = vertices.Count >= 2 ? vertices.ToArray() : Array.Empty<Vector2>();
            }

            private SpeedSample findSampleAtOrBefore(double time)
            {
                int lo = 0;
                int hi = samples.Count - 1;
                int best = 0;

                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;

                    if (samples[mid].Time <= time)
                    {
                        best = mid;
                        lo = mid + 1;
                    }
                    else
                        hi = mid - 1;
                }

                return samples[best];
            }

            private double mapDuration => mapEndTime - mapStartTime;

            private void resetSamples()
            {
                samples.Clear();
                hasRecordedTime = false;
                lastRecordedTime = mapStartTime - sample_interval_ms;
                lastSampledSpeed = speedSource.Value;
                path.Vertices = Array.Empty<Vector2>();
            }

            private void truncateSamplesAfter(double time, float drawWidth, float drawHeight)
            {
                int removeIndex = samples.FindIndex(s => s.Time > time);

                if (removeIndex >= 0)
                    samples.RemoveRange(removeIndex, samples.Count - removeIndex);

                lastRecordedTime = samples.Count > 0 ? samples[^1].Time : mapStartTime - sample_interval_ms;
                hasRecordedTime = samples.Count > 0;
                RebuildPath(drawWidth, drawHeight);
            }

            private static float timeToX(double time, float drawWidth, double mapStartTime, double mapDuration)
            {
                double progress = Math.Clamp((time - mapStartTime) / mapDuration, 0, 1);
                return (float)(drawWidth * progress);
            }

            private float timeToX(double time, float drawWidth) => timeToX(time, drawWidth, mapStartTime, mapDuration);

            private Vector2 computeEndpointPosition(float drawWidth, float drawHeight)
            {
                return new Vector2(
                    timeToX(currentTime, drawWidth),
                    mapSpeedToY(speedSource.Value, drawHeight));
            }

            private float mapSpeedToY(double speed, float drawHeight)
            {
                float y = MapSpeedToY(speed, drawHeight);
                return Math.Clamp(y, 0, drawHeight);
            }
        }
    }
}
