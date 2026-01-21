// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Components
{
    public partial class PlaybackControl : BottomBarContainer
    {
        private LoopPointButton setAButton = null!;
        private LoopPointButton setBButton = null!;
        private IconButton loopButton = null!;

        private IconButton playButton = null!;
        private PlaybackSpeedControl playbackSpeedControl = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Bindable<EditorScreenMode> currentScreenMode = new Bindable<EditorScreenMode>();
        private readonly BindableNumber<double> tempoAdjustment = new BindableDouble(1);
        private readonly BindableBool loopEnabled = new BindableBool();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, Editor? editor)
        {
            Background.Colour = colourProvider.Background4;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new Drawable[]
                    {
                        setAButton = new LoopPointButton("A")
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Scale = new Vector2(1.2f),
                            Action = setLoopStartToCurrentTime,
                        },
                        setBButton = new LoopPointButton("B")
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Scale = new Vector2(1.2f),
                            Action = setLoopEndToCurrentTime,
                        },
                        loopButton = new IconButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Scale = new Vector2(1.2f),
                            IconScale = new Vector2(1.2f),
                            Icon = FontAwesome.Solid.SyncAlt,
                            Action = toggleLoop,
                        },
                        playButton = new IconButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Scale = new Vector2(1.2f),
                            IconScale = new Vector2(1.2f),
                            Icon = FontAwesome.Regular.PlayCircle,
                            Action = togglePause,
                        },
                    },
                },
                playbackSpeedControl = new PlaybackSpeedControl
                {
                    AutoSizeAxes = Axes.Y,
                    Width = 180,
                    Padding = new MarginPadding { Left = 45, },
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = EditorStrings.PlaybackSpeed,
                        },
                        new PlaybackTabControl
                        {
                            Current = tempoAdjustment,
                            RelativeSizeAxes = Axes.X,
                            Height = 16,
                        },
                    }
                }
            };

            editorClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, tempoAdjustment);

            if (editor != null)
                currentScreenMode.BindTo(editor.Mode);

            loopEnabled.BindTo(editorClock.LoopEnabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentScreenMode.BindValueChanged(_ =>
            {
                if (currentScreenMode.Value == EditorScreenMode.Timing)
                {
                    tempoAdjustment.Value = 1;
                    tempoAdjustment.Disabled = true;
                    playbackSpeedControl.FadeTo(0.5f, 400, Easing.OutQuint);
                    playbackSpeedControl.TooltipText = "Speed adjustment is unavailable in timing mode. Timing at slower speeds is inaccurate due to resampling artifacts.";
                }
                else
                {
                    tempoAdjustment.Disabled = false;
                    playbackSpeedControl.FadeTo(1, 400, Easing.OutQuint);
                    playbackSpeedControl.TooltipText = default;
                }
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorClock.IsNotNull())
                editorClock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Tempo, tempoAdjustment);

            base.Dispose(isDisposing);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return false;

            switch (e.Key)
            {
                case Key.Space:
                    togglePause();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        private void togglePause()
        {
            if (editorClock.IsRunning)
                editorClock.Stop();
            else
                editorClock.Start();
        }

        private void toggleLoop()
        {
            loopEnabled.Value = !loopEnabled.Value;

            if (loopEnabled.Value)
            {
                // Prefer persisted session A/B range (ms). Only fall back to defaults when not available.
                if (LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                {
                    editorClock.SetLoopStartTime(editorClock.GetSnappedTime(startMs));
                    editorClock.SetLoopEndTime(editorClock.GetSnappedTime(endMs));
                }
                else
                {
                    // 默认范围：以当前活动光标为 A 起点，向后 设置 B 终点。
                    double currentTime = Math.Clamp(editorClock.CurrentTime, 0, editorClock.TrackLength);

                    double startTime = editorClock.GetSnappedTime(currentTime);
                    var timingPoint = editorClock.ControlPointInfo.TimingPointAt(startTime);

                    // 8 * (4/4 beat) = 8 beats.也就是8根白线。
                    double endTime = startTime + timingPoint.BeatLength * 8;
                    endTime = Math.Min(endTime, editorClock.TrackLength);
                    endTime = editorClock.GetSnappedTime(endTime);

                    if (endTime <= startTime)
                        endTime = Math.Min(editorClock.TrackLength, startTime + 1);

                    editorClock.SetLoopStartTime(startTime);
                    editorClock.SetLoopEndTime(endTime);
                }

                editorClock.Seek(editorClock.LoopStartTime.Value); // 跳转到开头
            }
        }

        private void setLoopStartToCurrentTime()
        {
            double currentTime = Math.Clamp(editorClock.CurrentTime, 0, editorClock.TrackLength);
            editorClock.SetLoopStartTime(editorClock.GetSnappedTime(currentTime));
            persistLoopRangeIfValid();
        }

        private void setLoopEndToCurrentTime()
        {
            double currentTime = Math.Clamp(editorClock.CurrentTime, 0, editorClock.TrackLength);
            editorClock.SetLoopEndTime(editorClock.GetSnappedTime(currentTime));
            persistLoopRangeIfValid();
        }

        private void persistLoopRangeIfValid()
        {
            double start = editorClock.LoopStartTime.Value;
            double end = editorClock.LoopEndTime.Value;

            if (end > start)
                LoopTimeRangeStore.Set(start, end);
        }

        private static readonly IconUsage play_icon = FontAwesome.Regular.PlayCircle;
        private static readonly IconUsage pause_icon = FontAwesome.Regular.PauseCircle;
        private static readonly IconUsage loop_on_icon = FontAwesome.Solid.Redo;
        private static readonly IconUsage loop_off_icon = FontAwesome.Regular.Circle;

        protected override void Update()
        {
            base.Update();

            playButton.Icon = editorClock.IsRunning ? pause_icon : play_icon;
            loopButton.Icon = loopEnabled.Value ? loop_on_icon : loop_off_icon;
        }

        private partial class LoopPointButton : OsuAnimatedButton
        {
            public LoopPointButton(string label)
                : base(HoverSampleSet.Button)
            {
                Size = new Vector2(IconButton.DEFAULT_BUTTON_SIZE);

                Add(new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                });
            }
        }

        private partial class PlaybackSpeedControl : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        public partial class PlaybackTabControl : OsuTabControl<double>
        {
            private static readonly double[] tempo_values = { 0.25, 0.5, 0.75, 1 };

            protected override TabItem<double> CreateTabItem(double value) => new PlaybackTabItem(value);

            protected override Dropdown<double> CreateDropdown() => null!;

            public PlaybackTabControl()
            {
                RelativeSizeAxes = Axes.Both;
                TabContainer.Spacing = Vector2.Zero;

                tempo_values.ForEach(AddItem);

                Current.Value = tempo_values.Last();
            }

            public partial class PlaybackTabItem : TabItem<double>
            {
                private const float fade_duration = 200;

                private readonly OsuSpriteText text;
                private readonly OsuSpriteText textBold;

                public PlaybackTabItem(double value)
                    : base(value)
                {
                    RelativeSizeAxes = Axes.Both;

                    Width = 1f / tempo_values.Length;

                    Children = new Drawable[]
                    {
                        text = new OsuSpriteText
                        {
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            Text = $"{value:0%}",
                            Font = OsuFont.GetFont(size: 14)
                        },
                        textBold = new OsuSpriteText
                        {
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            Text = $"{value:0%}",
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                            Alpha = 0,
                        },
                    };
                }

                private Color4 hoveredColour;
                private Color4 normalColour;

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    text.Colour = normalColour = colourProvider.Light3;
                    textBold.Colour = hoveredColour = colourProvider.Content1;
                }

                protected override bool OnHover(HoverEvent e)
                {
                    updateState();
                    return false;
                }

                protected override void OnHoverLost(HoverLostEvent e) => updateState();
                protected override void OnActivated() => updateState();
                protected override void OnDeactivated() => updateState();

                private void updateState()
                {
                    text.FadeColour(Active.Value || IsHovered ? hoveredColour : normalColour, fade_duration, Easing.OutQuint);
                    text.FadeTo(Active.Value ? 0 : 1, fade_duration, Easing.OutQuint);
                    textBold.FadeTo(Active.Value ? 1 : 0, fade_duration, Easing.OutQuint);
                }
            }
        }
    }
}
