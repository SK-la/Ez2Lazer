// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.EzLatency;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.EzOsuGame.Audio;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Screens.Utility
{
    /// <summary>
    /// 单轨道敲内置采样，测当前选中的音频驱动。离开时恢复进入前的驱动。
    /// </summary>
    public partial class EzAudioDriverLatencyScreen : OsuScreen
    {
        public static readonly LocalisableString BUTTON_TEXT = new EzLocalizationManager.EzLocalisableString(
            "运行音频驱动延迟验证", "Run audio driver latency check");

        private const string hit_sample_name = @"UI/button-select";
        private const int notes_per_run = 8;
        private const double note_interval = 600;
        private const double approach = 1000;
        private const double switch_settle_ms = 900;

        public override bool HideOverlaysOnEnter => true;

        public override float BackgroundParallaxAmount => 0;

        public override bool AcceptsFocus => true;

        private readonly BindableBool autoHit = new BindableBool(true);
        private readonly Bindable<string> selectedDevice = new Bindable<string>(string.Empty);

        private AudioManager audio = null!;
        private MusicController music = null!;
        private ISampleStore? hitSamples;
        private ISample? hitSample;

        private string originalDevice = string.Empty;
        private int roundToken;
        private int hits;
        private bool cleaned;
        private bool roundActive;
        private bool applyingDevice;
        private bool outputReady = true;

        private readonly HashSet<InputKey> gameplayKeys = new HashSet<InputKey>();

        private OsuSpriteText status = null!;
        private Container noteLayer = null!;
        private CompositeDrawable leftPanel = null!;
        private FillFlowContainer resultsFlow = null!;
        private DeviceDropdown deviceDropdown = null!;
        private OsuColour colours = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, MusicController music, RealmAccess realm, RulesetStore rulesets, OsuColour colours)
        {
            this.audio = audio;
            this.music = music;
            this.colours = colours;

            hitSamples = audio.GetSampleStore(applyGlobalAdjustments: false);
            hitSamples.AddExtension(@"ogg");

            originalDevice = audio.AudioDevice.Value ?? string.Empty;
            selectedDevice.Value = originalDevice;
            collectGameplayKeys(realm, rulesets);
            reloadHitSample();

            var devices = new List<string> { string.Empty };
            devices.AddRange(audio.AudioDeviceNames.Where(name => !string.IsNullOrEmpty(name)));

            if (!string.IsNullOrEmpty(originalDevice) && devices.All(name => name != originalDevice))
                devices.Add(originalDevice);

            deviceDropdown = new DeviceDropdown
            {
                RelativeSizeAxes = Axes.X,
                Items = devices.Distinct(StringComparer.Ordinal).ToList(),
                Current = selectedDevice,
            };

            var controls = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = new EzLocalizationManager.EzLocalisableString("音频驱动", "Audio device"),
                        Font = OsuFont.Default.With(size: 20),
                    },
                    deviceDropdown,
                    new QuietCheckbox
                    {
                        RelativeSizeAxes = Axes.X,
                        LabelText = new EzLocalizationManager.EzLocalisableString("自动打", "Auto-hit"),
                        Current = autoHit,
                    },
                    new ClickableContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Masking = true,
                        CornerRadius = 10,
                        CornerExponent = 2.5f,
                        Action = () =>
                        {
                            if (!cleaned && outputReady)
                                beginRun(settle: false);
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colours.Blue3,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = new EzLocalizationManager.EzLocalisableString("再试一次", "Try again"),
                                Font = OsuFont.Default.With(size: 16),
                            },
                        },
                    },
                },
            };

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray1,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 400),
                        new Dimension(),
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            leftPanel = new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Top = 20, Left = 16, Right = 12, Bottom = 16 },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new Drawable[] { controls },
                                    new Drawable[]
                                    {
                                        new OsuScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Child = resultsFlow = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                                Padding = new MarginPadding { Right = 14 },
                                            },
                                        },
                                    },
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = new Container
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Y,
                                    Height = 0.72f,
                                    Width = 132,
                                    Masking = true,
                                    CornerRadius = 8,
                                    CornerExponent = 2.5f,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.Gray3,
                                        },
                                        noteLayer = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding { Bottom = 28 },
                                            Masking = true,
                                        },
                                        new Box
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            RelativeSizeAxes = Axes.X,
                                            Height = 4,
                                            Margin = new MarginPadding { Bottom = 28 },
                                            Colour = Color4.White,
                                        },
                                        status = new OsuSpriteText
                                        {
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                            Margin = new MarginPadding { Bottom = 6 },
                                            Font = OsuFont.Default.With(size: 14),
                                            Colour = colours.GrayC,
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            music.Stop();
            audio.OnAudioOutputSwitchFailed += onAudioFailed;
            selectedDevice.BindValueChanged(onDeviceSelected);

            EzLatencyManager.GLOBAL.SetAcousticThreshold(InputAudioLatencyTracker.AcousticRmsThreshold);
            EzLatencyManager.GLOBAL.ClearStatistics();
            EzLatencyManager.GLOBAL.Enabled.Value = true;

            Schedule(() => GetContainingFocusManager()?.ChangeFocus(this));
            beginRun(settle: false);
        }

        protected override void Update()
        {
            base.Update();

            if (!outputReady || !roundActive)
                return;

            double now = Time.Current;
            float line = noteLayer.ChildSize.Y;

            foreach (var note in noteLayer.Children.OfType<LaneNote>())
            {
                float span = Math.Max(0, line - note.Height);
                float remaining = (float)(note.HitTime - now);
                note.Y = line - remaining / (float)approach * span;
                note.Alpha = note.Consumed ? 0.25f : 1;
            }

            if (!autoHit.Value)
                return;

            var due = noteLayer.Children.OfType<LaneNote>().FirstOrDefault(note => !note.Consumed);
            if (due != null && now >= due.HitTime)
                registerHit();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape || e.Repeat || autoHit.Value || !outputReady || !roundActive)
                return false;

            if (!gameplayKeys.Contains(KeyCombination.FromKey(e.Key)))
                return false;

            registerHit();
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (autoHit.Value || !outputReady || !roundActive || leftPanel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                return base.OnMouseDown(e);

            if (!gameplayKeys.Contains(KeyCombination.FromMouseButton(e.Button)))
                return base.OnMouseDown(e);

            registerHit();
            return true;
        }

        private void onDeviceSelected(ValueChangedEvent<string> change)
        {
            if (applyingDevice || cleaned)
                return;

            beginRun(settle: !string.Equals(audio.AudioDevice.Value ?? string.Empty, change.NewValue ?? string.Empty, StringComparison.Ordinal));
        }

        private void beginRun(bool settle)
        {
            hits = 0;
            roundActive = false;
            noteLayer.Clear();
            outputReady = !settle;
            status.Text = settle ? "切换输出…" : $"0/{notes_per_run}";

            EzLatencyManager.GLOBAL.ClearStatistics();
            EzLatencyManager.GLOBAL.SetAcousticThreshold(InputAudioLatencyTracker.AcousticRmsThreshold);

            string device = selectedDevice.Value ?? string.Empty;
            int token = ++roundToken;

            if (settle && !string.Equals(audio.AudioDevice.Value ?? string.Empty, device, StringComparison.Ordinal))
                audio.AudioDevice.Value = device;

            if (!settle)
            {
                armRun(token);
                return;
            }

            Scheduler.AddDelayed(() =>
            {
                if (token != roundToken)
                    return;

                armRun(token);
            }, switch_settle_ms);
        }

        private void armRun(int token)
        {
            if (token != roundToken)
                return;

            reloadHitSample();
            outputReady = true;
            roundActive = true;
            hits = 0;

            double start = Time.Current + approach;
            noteLayer.Clear();

            for (int i = 0; i < notes_per_run; i++)
            {
                noteLayer.Add(new LaneNote
                {
                    HitTime = start + i * note_interval,
                });
            }

            status.Text = hitSample == null ? "内置采样没加载到" : $"0/{notes_per_run}";
        }

        private void registerHit()
        {
            if (!outputReady || !roundActive || hits >= notes_per_run)
                return;

            EzLatencyManager.GLOBAL.SetAcousticThreshold(InputAudioLatencyTracker.AcousticRmsThreshold);
            EzLatencyManager.GLOBAL.RecordInputEvent(hits);
            playHit();

            var note = noteLayer.Children.OfType<LaneNote>().FirstOrDefault(candidate => !candidate.Consumed);
            note?.Consumed = true;

            hits++;
            status.Text = $"{hits}/{notes_per_run}";

            if (hits < notes_per_run)
                return;

            appendResult();
            roundActive = false;
            hits = 0;
            EzLatencyManager.GLOBAL.ClearStatistics();
            status.Text = "本轮结束";
        }

        private void appendResult()
        {
            var stats = EzLatencyManager.GLOBAL.GetStatistics();
            string detail = stats.HasData
                ? $"n={stats.RecordCount}  In→Play {stats.AvgInputToPlayback:0.0}/{stats.MinInputToPlayback:0.0}/{stats.MaxInputToPlayback:0.0} ms"
                  + (stats.AcousticRecordCount > 0
                      ? $"\nIn→Acou {stats.AvgAcousticRoundtrip:0.0}/{stats.MinAcousticRoundtrip:0.0}/{stats.MaxAcousticRoundtrip:0.0} ms (n={stats.AcousticRecordCount})"
                      : "\nIn→Acou 无")
                : "没有完整记录";

            addResultCard(deviceLabel(selectedDevice.Value), detail);
        }

        private void onAudioFailed(AudioManager.AudioOutputFailureInfo info)
        {
            Schedule(() =>
            {
                if (cleaned || !string.Equals(info.Selection ?? string.Empty, selectedDevice.Value ?? string.Empty, StringComparison.Ordinal))
                    return;

                outputReady = false;
                status.Text = "这个输出没有起来";
                addResultCard(deviceLabel(selectedDevice.Value), info.Message);
            });
        }

        private void addResultCard(string name, string detail)
        {
            resultsFlow.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 6,
                CornerExponent = 2.5f,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Gray3,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new ScrollingLabel(name),
                            new OsuSpriteText
                            {
                                Text = detail,
                                Font = OsuFont.Default.With(size: 13),
                                Colour = colours.GrayB,
                                AllowMultiline = true,
                                RelativeSizeAxes = Axes.X,
                            },
                        },
                    },
                },
            });
        }

        private void reloadHitSample()
        {
            hitSamples?.Invalidate(hit_sample_name);
            hitSample = hitSamples?.Get(hit_sample_name);
        }

        private void playHit()
        {
            if (hitSample == null)
            {
                status.Text = "内置采样没加载到";
                return;
            }

            var channel = hitSample.GetChannel();
            channel.Volume.Value = 1;
            channel.Play();
        }

        private void collectGameplayKeys(RealmAccess realm, RulesetStore rulesets)
        {
            realm.Run(context =>
            {
                foreach (var binding in context.All<RealmKeyBinding>().Where(binding => !string.IsNullOrEmpty(binding.RulesetName)))
                    addKeys(binding.KeyCombination);
            });

            foreach (var info in rulesets.AvailableRulesets)
            {
                var ruleset = info.CreateInstance();
                var variants = new HashSet<int> { 0 };

                foreach (int variant in ruleset.GameplayVariants)
                    variants.Add(variant);

                foreach (int variant in variants)
                {
                    foreach (var binding in ruleset.GetDefaultKeyBindings(variant))
                        addKeys(binding.KeyCombination);
                }
            }
        }

        private void addKeys(KeyCombination combination)
        {
            foreach (var key in combination.Keys)
            {
                if (key != InputKey.None)
                    gameplayKeys.Add(key);
            }
        }

        private static string deviceLabel(string? device) => string.IsNullOrEmpty(device) ? "默认" : device;

        public override bool OnExiting(ScreenExitEvent e)
        {
            cleanup();
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            cleanup();
            base.Dispose(isDisposing);
        }

        private void cleanup()
        {
            if (cleaned)
                return;

            cleaned = true;
            roundToken++;
            selectedDevice.ValueChanged -= onDeviceSelected;
            audio.OnAudioOutputSwitchFailed -= onAudioFailed;

            applyingDevice = true;
            audio.AudioDevice.Value = originalDevice;

            EzLatencyManager.GLOBAL.Enabled.Value = false;
            EzLatencyManager.GLOBAL.ClearStatistics();
            hitSamples?.Dispose();
            hitSamples = null;
        }

        private partial class LaneNote : CompositeDrawable
        {
            public double HitTime { get; init; }

            public bool Consumed { get; set; }

            public LaneNote()
            {
                Anchor = Anchor.TopCentre;
                Origin = Anchor.BottomCentre;
                RelativeSizeAxes = Axes.X;
                Width = 0.86f;
                Height = 22;
                Masking = true;
                CornerRadius = 3;

                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(102, 204, 255, 255),
                };
            }
        }

        private partial class ScrollingLabel : CompositeDrawable
        {
            private readonly OsuSpriteText text;

            public ScrollingLabel(string value)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Masking = true;

                InternalChild = text = new OsuSpriteText
                {
                    Text = value,
                    Font = OsuFont.Default.With(size: 16),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };
            }

            protected override void Update()
            {
                base.Update();

                float overflow = text.DrawWidth - DrawWidth;

                if (overflow <= 2)
                {
                    text.X = 0;
                    return;
                }

                const double pause = 700;
                double travel = Math.Max(1400, overflow * 18);
                double cycle = pause + travel + pause + travel;
                double t = Time.Current % cycle;

                if (t < pause)
                    text.X = 0;
                else if (t < pause + travel)
                    text.X = (float)(-(t - pause) / travel * overflow);
                else if (t < pause + travel + pause)
                    text.X = -overflow;
                else
                    text.X = (float)(-(1 - (t - pause - travel - pause) / travel) * overflow);
            }
        }

        private partial class QuietCheckbox : OsuCheckbox
        {
            protected override bool PlaySoundsOnUserChange => false;
        }

        private partial class DeviceDropdown : OsuDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => string.IsNullOrEmpty(item) ? "默认" : item;
        }
    }
}
