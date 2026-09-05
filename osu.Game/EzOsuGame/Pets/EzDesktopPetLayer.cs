// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Independent desktop-pet layer. Not a skin component, not an Ez layout component,
    /// and not an <c>OsuFocusedOverlayContainer</c>. Visibility is the master toggle,
    /// per-scene checkboxes, and optional pet.json hide/show rules.
    /// </summary>
    public partial class EzDesktopPetLayer : VisibilityContainer
    {
        private enum PetScene
        {
            Other,
            Menu,
            SongSelect,
            Gameplay,
            Results,
        }

        private const float missing_pack_width = 320;
        private const float missing_pack_height = 88;

        private readonly EzPetStateMachine stateMachine = new EzPetStateMachine();
        private readonly Dictionary<string, Texture[]> clipTextures = new Dictionary<string, Texture[]>(StringComparer.Ordinal);
        private readonly EzPetMotionDriver motionDriver = new EzPetMotionDriver();
        private string? motionOwnerState;
        private string? activeMotionMode;
        private double live2DClipStartTime = double.MaxValue;

        private Bindable<bool> enabled = null!;
        private Bindable<string> packName = null!;
        private Bindable<double> scale = null!;
        private Bindable<float> posX = null!;
        private Bindable<float> posY = null!;
        private Bindable<bool> showOnMenu = null!;
        private Bindable<bool> showOnSongSelect = null!;
        private Bindable<bool> showOnGameplay = null!;
        private Bindable<bool> showOnResults = null!;

        private IBindable<WorkingBeatmap> beatmap = null!;

        private EzPetPackLoader loader = null!;
        private EzResourceStore resources = null!;
        private OsuGame? game;

        private Container petBox = null!;
        private TextureAnimation animation = null!;
        private EzPetLive2DHost live2DHost = null!;
        private EzPetCubismSession? cubismSession;
        private Container missingPackPanel = null!;
        private bool showingMissingPack;
        private bool preferLive2DHost;

        [Resolved(canBeNull: true)]
        private IdleTracker? idleTracker { get; set; }

        private EzPetPack? currentPack;
        private bool inGameplay;
        private bool eventHidden;
        private PetScene currentScene;
        private bool hovered;
        private bool dragging;
        private bool suppressPositionApply;
        private Guid lastStarBeatmapId;
        private double lastStarRating = double.NaN;
        private ScoreProcessor? boundScoreProcessor;
        private Player? boundPlayer;

        protected override bool StartHidden => false;

        public EzDesktopPetLayer()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            // Hidden VisibilityContainers skip Scheduler/Update. Screen changes then never unhide the pet.
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, Storage storage, EzResourceStore resourceStore, IBindable<WorkingBeatmap> workingBeatmap, OsuGame? osuGame)
        {
            resources = resourceStore;
            game = osuGame ?? this.FindClosestParent<OsuGame>();
            beatmap = workingBeatmap.GetBoundCopy();

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetEnabled);
            packName = ezConfig.GetBindable<string>(Ez2Setting.DesktopPetPack);
            scale = ezConfig.GetBindable<double>(Ez2Setting.DesktopPetScale);
            posX = ezConfig.GetBindable<float>(Ez2Setting.DesktopPetPositionX);
            posY = ezConfig.GetBindable<float>(Ez2Setting.DesktopPetPositionY);
            showOnMenu = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnMenu);
            showOnSongSelect = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnSongSelect);
            showOnGameplay = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnGameplay);
            showOnResults = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnResults);

            loader = new EzPetPackLoader(storage);
            loader.EnsureDefaultPack();

            InternalChild = petBox = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.None,
                Children = new Drawable[]
                {
                    animation = new TextureAnimation(startAtCurrentTime: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fit,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    live2DHost = new EzPetLive2DHost
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    missingPackPanel = createMissingPackPanel(),
                },
            };
        }

        private static Container createMissingPackPanel() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("1a1a1f"),
                    Alpha = 0.92f,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding(12),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "当前没有可用的宠物包",
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                            Colour = Colour4.White,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "No pet pack available",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Colour4.FromHex("b0b0b8"),
                        },
                    },
                },
            },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stateMachine.ClipChanged += onClipChanged;
            stateMachine.VisibilityAction += onVisibilityAction;
            stateMachine.MotionRequested += onMotionRequested;

            enabled.BindValueChanged(_ => updateVisibility(), false);
            showOnMenu.BindValueChanged(_ => updateVisibility(), false);
            showOnSongSelect.BindValueChanged(_ => updateVisibility(), false);
            showOnGameplay.BindValueChanged(_ => updateVisibility(), false);
            showOnResults.BindValueChanged(_ => updateVisibility(), false);
            packName.BindValueChanged(_ => reloadPack(), true);
            scale.BindValueChanged(_ => applyScale(), true);
            posX.BindValueChanged(_ => onPositionBindableChanged(), true);
            posY.BindValueChanged(_ => onPositionBindableChanged(), false);
            beatmap.BindValueChanged(_ => onBeatmapChanged(), true);

            game ??= this.FindClosestParent<OsuGame>();

            if (game != null)
            {
                game.ScreenStack.ScreenPushed += onScreenChanged;
                game.ScreenStack.ScreenExited += onScreenChanged;
                applyCurrentScreen(game.ScreenStack.CurrentScreen);
            }

            updateVisibility();
            applyPosition();
        }

        protected override void PopIn() => this.FadeIn(120, Easing.OutQuad);

        protected override void PopOut() => this.FadeOut(120, Easing.OutQuad);

        protected override void Update()
        {
            if (!enabled.Value)
            {
                if (State.Value == Visibility.Visible)
                    Hide();

                return;
            }

            base.Update();

            if (!dragging)
            {
                if (motionDriver.IsActive)
                    updateMotion(Time.Elapsed);
                else
                    applyPosition();
            }

            pollHover();

            // Hardware idle (IdleTracker): only accumulate AFK idle clips outside gameplay.
            if (idleTracker != null && !idleTracker.IsIdle.Value)
                stateMachine.NotifyUserActivity();
            else if (!inGameplay)
                stateMachine.UpdateIdle(Time.Elapsed / 1000.0);

            checkClipFinished();

            if (preferLive2DHost && cubismSession?.IsReady == true)
            {
                cubismSession.Update(Time.Elapsed / 1000.0);
                live2DHost.ApplyBreath(cubismSession.BreathValue);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (!enabled.Value || State.Value != Visibility.Visible)
                return false;

            if (!petBox.ReceivePositionalInputAt(screenSpacePos))
                return false;

            // Hover reactions use pollHover (petBox hit-test). Clicks/drags only while LAlt is held
            // (or mid-drag) so the pet never blocks UI underneath.
            if (dragging || isLeftAltHeld())
                return true;

            return false;
        }

        protected override bool ReceivePositionalInputAtSubTree(Vector2 screenSpacePos)
            => ReceivePositionalInputAt(screenSpacePos);

        protected override void Dispose(bool isDisposing)
        {
            if (game != null)
            {
                game.ScreenStack.ScreenPushed -= onScreenChanged;
                game.ScreenStack.ScreenExited -= onScreenChanged;
            }

            unbindPlayer();
            stateMachine.ClipChanged -= onClipChanged;
            stateMachine.VisibilityAction -= onVisibilityAction;
            stateMachine.MotionRequested -= onMotionRequested;
            motionDriver.Stop();
            animation.ClearFrames();
            clipTextures.Clear();
            cubismSession?.Dispose();
            cubismSession = null;

            base.Dispose(isDisposing);
        }

        private void reloadPack()
        {
            clipTextures.Clear();
            motionDriver.Stop();
            cubismSession?.Dispose();
            cubismSession = null;
            currentPack = null;
            eventHidden = false;
            preferLive2DHost = false;

            try
            {
                string name = string.IsNullOrWhiteSpace(packName.Value) ? EzDefaultPetPack.NAME : packName.Value;
                currentPack = loader.Load(name) ?? loader.Load(EzDefaultPetPack.NAME);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez pet pack '{packName.Value}'");
                currentPack = null;
            }

            if (currentPack == null)
            {
                var definition = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);
                currentPack = new EzPetPack
                {
                    Name = EzDefaultPetPack.NAME,
                    Definition = definition,
                    AvailableClips = new HashSet<string>(StringComparer.Ordinal),
                };
            }

            if (currentPack.Live2DAuthorized)
            {
                string? cubismError = null;
                if (EzPetCubismSession.TryCreate(loader.PetsStorage, currentPack.Live2DModelEntryPath, out var session, out cubismError))
                    cubismSession = session;

                // Prefer Cubism host when Core works; else PNG frames if present; else setup placeholder.
                preferLive2DHost = cubismSession?.IsReady == true || !currentPack.HasRasterFrames;
                live2DHost.BindPack(currentPack, currentPack.Live2DModelEntryPath, cubismSession, loader.PetsStorage, cubismError);
                Logger.Log(
                    $"Ez pet pack '{currentPack.Name}' Live2D authorised; cubism={(cubismSession?.IsReady == true ? "ready" : cubismError ?? "unavailable")}.",
                    LoggingTarget.Runtime);
            }

            showingMissingPack = currentPack.AvailableClips.Count == 0 && !currentPack.Live2DAuthorized;
            setMissingPackVisible(showingMissingPack);

            if (showingMissingPack)
            {
                animation.ClearFrames();
                animation.IsPlaying = false;
            }
            else
            {
                stateMachine.ApplyPack(currentPack.Definition, currentPack.AvailableClips);
            }

            applyBlendMode(null);
            applyScale();
        }

        private bool ensureClipLoaded(string clipName)
        {
            if (clipTextures.ContainsKey(clipName))
                return true;

            if (currentPack == null || !currentPack.AvailableClips.Contains(clipName))
                return false;

            if (!currentPack.Definition.Clips.TryGetValue(clipName, out var clip))
                return false;

            try
            {
                var frames = loadClipFrames(currentPack, clipName, clip);
                if (frames.Length == 0)
                    return false;

                clipTextures[clipName] = frames;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez pet clip '{currentPack.Name}/{clipName}'");
                return false;
            }
        }

        private void unloadUnusedClips(string keepClip)
        {
            string idleClip = currentPack?.Definition.DefaultState ?? "idle";
            var remove = new List<string>();

            foreach (string key in clipTextures.Keys)
            {
                if (string.Equals(key, keepClip, StringComparison.Ordinal))
                    continue;
                if (string.Equals(key, idleClip, StringComparison.Ordinal))
                    continue;

                remove.Add(key);
            }

            foreach (string key in remove)
                clipTextures.Remove(key);
        }

        private Texture[] loadClipFrames(EzPetPack pack, string clipName, EzPetClipDefinition clip)
        {
            var names = loader.GetClipFrameNames(pack.Name, clipName, clip);
            var textures = new List<Texture>(names.Count);

            foreach (string frameName in names)
            {
                try
                {
                    var texture = resources.Get($"Pets/{pack.Name}/{frameName}", EzTextureUsage.AnimationSafe);
                    if (texture != null)
                        textures.Add(texture);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to decode Ez pet frame '{pack.Name}/{frameName}'");
                }
            }

            if (names.Count > 0 && textures.Count == 0)
            {
                Logger.Log(
                    $"Ez pet pack '{pack.Name}' clip '{clipName}' has {names.Count} frame files but none could be decoded.",
                    LoggingTarget.Runtime,
                    LogLevel.Error);
            }

            return textures.Count > 0 ? textures.ToArray() : [];
        }

        private bool usingLive2DHost => preferLive2DHost && currentPack?.Live2DAuthorized == true;

        private void setMissingPackVisible(bool visible)
        {
            missingPackPanel.Alpha = visible ? 1 : 0;

            if (usingLive2DHost && !visible)
            {
                live2DHost.Alpha = 1;
                animation.Alpha = 0;
                animation.IsPlaying = false;
            }
            else
            {
                live2DHost.Alpha = 0;
                animation.Alpha = visible ? 0 : 1;
            }
        }

        private void onClipChanged(string state, string clip)
        {
            if (showingMissingPack)
                return;

            if (usingLive2DHost)
            {
                animation.ClearFrames();
                animation.IsPlaying = false;
                live2DClipStartTime = Time.Current;
                live2DHost.NotifyState(state, clip, cubismSession);
                setMissingPackVisible(false);
                applyScale();
                stopOrphanedWanderMotion();
                return;
            }

            animation.ClearFrames();

            if (!ensureClipLoaded(clip) || !clipTextures.TryGetValue(clip, out var frames) || frames.Length == 0)
            {
                string idleClip = currentPack?.Definition.DefaultState ?? "idle";

                if (!ensureClipLoaded(idleClip) || !clipTextures.TryGetValue(idleClip, out frames) || frames.Length == 0)
                {
                    showingMissingPack = true;
                    setMissingPackVisible(true);
                    applyScale();
                    return;
                }

                clip = idleClip;
            }

            unloadUnusedClips(clip);
            setMissingPackVisible(false);

            EzPetClipDefinition? clipDef = null;
            currentPack?.Definition.Clips.TryGetValue(clip, out clipDef);

            bool loop = clipDef?.Loop == true;
            double fps = clipDef is { Fps: > 0 } ? clipDef.Fps : 12;

            animation.Loop = loop;
            animation.DefaultFrameLength = 1000.0 / fps;

            foreach (var frame in frames)
                animation.AddFrame(frame);

            animation.Seek(0);
            animation.IsPlaying = true;
            applyBlendMode(clipDef);
            applyScale();
            stopOrphanedWanderMotion();
        }

        private void stopOrphanedWanderMotion()
        {
            if (!motionDriver.IsActive)
                return;

            if (!string.Equals(activeMotionMode, "wander", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(stateMachine.CurrentState, motionOwnerState, StringComparison.OrdinalIgnoreCase))
                return;

            motionDriver.Stop();
            motionOwnerState = null;
            activeMotionMode = null;
            persistPosition();
        }

        private void onMotionRequested(string? motionId)
        {
            if (string.IsNullOrWhiteSpace(motionId)
                || currentPack == null
                || !currentPack.Definition.Motions.TryGetValue(motionId, out var motion))
            {
                motionDriver.Stop();
                motionOwnerState = null;
                activeMotionMode = null;
                return;
            }

            motionOwnerState = stateMachine.CurrentState;
            activeMotionMode = motion.Mode;
            motionDriver.Start(motion, getNormalisedPosition(), resolveMotionAnchor);
        }

        private void updateMotion(double elapsedMs)
        {
            if (DrawSize.X <= 0 || DrawSize.Y <= 0)
                return;

            var next = motionDriver.Update(elapsedMs, getNormalisedPosition());

            if (next == null)
            {
                persistPosition();
                return;
            }

            petBox.Position = new Vector2(next.Value.X * DrawSize.X, next.Value.Y * DrawSize.Y);

            if (!motionDriver.IsActive)
                persistPosition();
        }

        private Vector2 getNormalisedPosition()
        {
            if (DrawSize.X <= 0 || DrawSize.Y <= 0)
                return new Vector2(Math.Clamp(posX.Value, 0f, 1f), Math.Clamp(posY.Value, 0f, 1f));

            return new Vector2(
                Math.Clamp(petBox.Position.X / DrawSize.X, 0f, 1f),
                Math.Clamp(petBox.Position.Y / DrawSize.Y, 0f, 1f));
        }

        private Vector2? resolveMotionAnchor(string anchor)
        {
            if (string.Equals(anchor, "results.rank", StringComparison.OrdinalIgnoreCase))
            {
                // Heuristic: expanded results panel rank sits near upper-centre. Exact HUD query can come later.
                return new Vector2(0.50f, 0.38f);
            }

            return null;
        }

        private void applyBlendMode(EzPetClipDefinition? clipDef)
        {
            string? packMode = currentPack?.Definition.BlendMode;
            string? clipMode = clipDef?.BlendMode;
            animation.Blending = EzPetBlendModeExtensions.Resolve(packMode, clipMode).ToBlendingParameters();
        }

        private void checkClipFinished()
        {
            if (showingMissingPack)
                return;

            if (usingLive2DHost)
            {
                if (currentPack?.Definition.Clips.TryGetValue(stateMachine.CurrentClip, out var clip) == true
                    && clip is { Loop: false }
                    && Time.Current - live2DClipStartTime >= 800)
                {
                    live2DClipStartTime = double.MaxValue;
                    stateMachine.NotifyClipFinished();
                }

                return;
            }

            if (animation.Loop || animation.FrameCount == 0 || animation.Duration <= 0)
                return;

            if (animation.PlaybackPosition >= animation.Duration)
                stateMachine.NotifyClipFinished();
        }

        private void applyScale()
        {
            float s = (float)Math.Clamp(scale.Value, 0.25, 3.0);

            if (showingMissingPack)
            {
                petBox.Size = new Vector2(missing_pack_width * s, missing_pack_height * s);
                return;
            }

            if (usingLive2DHost)
            {
                const float live2d_base = 256;
                petBox.Size = new Vector2(live2d_base * s, live2d_base * s);
                return;
            }

            float width = missing_pack_width * s;
            float height = missing_pack_height * s;

            if (animation.FrameCount > 0)
            {
                var frame = animation.CurrentFrame;

                if (frame != null && frame.Available && frame.Width > 0 && frame.Height > 0)
                {
                    width = frame.Width * s;
                    height = frame.Height * s;
                }
            }

            petBox.Size = new Vector2(width, height);
        }

        private void onPositionBindableChanged()
        {
            if (!suppressPositionApply)
                applyPosition();
        }

        private void applyPosition()
        {
            if (DrawSize.X <= 0 || DrawSize.Y <= 0)
                return;

            petBox.Position = new Vector2(
                Math.Clamp(posX.Value, 0f, 1f) * DrawSize.X,
                Math.Clamp(posY.Value, 0f, 1f) * DrawSize.Y);
        }

        private void persistPosition()
        {
            if (DrawSize.X <= 0 || DrawSize.Y <= 0)
                return;

            // Read both axes before writing bindables. Setting posX first would fire
            // applyPosition with the old posY and clobber the dragged Y before we save it.
            float newX = Math.Clamp(petBox.Position.X / DrawSize.X, 0f, 1f);
            float newY = Math.Clamp(petBox.Position.Y / DrawSize.Y, 0f, 1f);

            suppressPositionApply = true;
            posX.Value = newX;
            posY.Value = newY;
            suppressPositionApply = false;
            applyPosition();
        }

        private void updateVisibility()
        {
            bool show = enabled.Value && !eventHidden && sceneAllowsDisplay();

            if (show)
                Show();
            else
                Hide();
        }

        private bool sceneAllowsDisplay() => currentScene switch
        {
            PetScene.Menu => showOnMenu.Value,
            PetScene.SongSelect => showOnSongSelect.Value,
            PetScene.Gameplay => showOnGameplay.Value,
            PetScene.Results => showOnResults.Value,
            _ => false,
        };

        private void onVisibilityAction(EzPetVisibilityAction action)
        {
            eventHidden = action == EzPetVisibilityAction.Hide;
            updateVisibility();
        }

        private void pollHover()
        {
            if (dragging || State.Value != Visibility.Visible)
                return;

            var input = GetContainingInputManager();
            if (input == null)
                return;

            bool nowHovered = petBox.ReceivePositionalInputAt(input.CurrentState.Mouse.Position);

            if (nowHovered == hovered)
                return;

            hovered = nowHovered;

            if (hovered)
                stateMachine.HandleHover();
            else
                stateMachine.HandleHoverEnd();
        }

        private bool isLeftAltHeld()
        {
            var input = GetContainingInputManager();
            return input != null && input.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt);
        }
    }
}
