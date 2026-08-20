// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

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
        }

        private const float placeholder_size = 128;
        private const int placeholder_frames = 4;

        private readonly EzPetStateMachine stateMachine = new EzPetStateMachine();
        private readonly Dictionary<string, Texture[]> clipTextures = new Dictionary<string, Texture[]>(StringComparer.Ordinal);

        private Bindable<bool> enabled = null!;
        private Bindable<string> packName = null!;
        private Bindable<double> scale = null!;
        private Bindable<float> posX = null!;
        private Bindable<float> posY = null!;
        private Bindable<bool> showOnMenu = null!;
        private Bindable<bool> showOnSongSelect = null!;
        private Bindable<bool> showOnGameplay = null!;

        private IBindable<WorkingBeatmap> beatmap = null!;

        private EzPetPackLoader loader = null!;
        private EzResourceStore resources = null!;
        private IRenderer renderer = null!;
        private TextureStore petTextures = null!;
        private OsuGame? game;

        private Container petBox = null!;
        private TextureAnimation animation = null!;

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
            renderer = resourceStore.Renderer;
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

            loader = new EzPetPackLoader(storage);
            loader.EnsureDefaultPack();

            // TextureAnimation/Sprite disposes the previous frame on each switch.
            // LargeTextureStore's TextureWithRefCount then purges the native texture and crashes
            // on the next loop. A non-atlas TextureStore keeps frames alive (Dispose is a no-op).
            petTextures = new TextureStore(
                renderer,
                resources.CreateTextureLoaderStore(resources.Files),
                useAtlas: false,
                scaleAdjust: 1);

            InternalChild = petBox = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.None,
                Child = animation = new TextureAnimation(startAtCurrentTime: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stateMachine.ClipChanged += onClipChanged;
            stateMachine.VisibilityAction += onVisibilityAction;

            enabled.BindValueChanged(_ => updateVisibility(), false);
            showOnMenu.BindValueChanged(_ => updateVisibility(), false);
            showOnSongSelect.BindValueChanged(_ => updateVisibility(), false);
            showOnGameplay.BindValueChanged(_ => updateVisibility(), false);
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
                applyPosition();

            pollHover();
            stateMachine.UpdateIdle(Time.Elapsed / 1000.0);
            checkClipFinished();
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (!enabled.Value || State.Value != Visibility.Visible)
                return false;

            if (!petBox.ReceivePositionalInputAt(screenSpacePos))
                return false;

            // Gameplay: pass through ordinary clicks so notes still receive input. LAlt drag still hits.
            if (inGameplay && !isLeftAltHeld())
                return false;

            return true;
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
            animation.ClearFrames();
            clipTextures.Clear();
            petTextures.Dispose();

            base.Dispose(isDisposing);
        }

        private void reloadPack()
        {
            clipTextures.Clear();
            currentPack = null;
            eventHidden = false;

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
                    AvailableClips = new HashSet<string>(definition.Clips.Keys, StringComparer.Ordinal),
                };
            }

            try
            {
                preloadClips(currentPack);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to preload Ez pet pack '{currentPack.Name}'");
            }

            stateMachine.ApplyPack(currentPack.Definition, currentPack.AvailableClips);
            applyBlendMode(null);
            applyScale();
        }

        private void preloadClips(EzPetPack pack)
        {
            foreach ((string clipName, var clip) in pack.Definition.Clips)
            {
                if (!pack.AvailableClips.Contains(clipName))
                    continue;

                var frames = loadClipFrames(pack, clipName, clip);
                if (frames.Length == 0)
                    frames = createPlaceholderFrames(clipName);

                clipTextures[clipName] = frames;
            }
        }

        private Texture[] loadClipFrames(EzPetPack pack, string clipName, EzPetClipDefinition clip)
        {
            var names = loader.GetClipFrameNames(pack.Name, clipName, clip);
            var textures = new List<Texture>(names.Count);

            foreach (string frameName in names)
            {
                try
                {
                    var texture = petTextures.Get($"Pets/{pack.Name}/{frameName}");
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

        private Texture[] createPlaceholderFrames(string clipName)
        {
            var baseColour = placeholderColour(clipName);
            var frames = new Texture[placeholder_frames];

            for (int i = 0; i < placeholder_frames; i++)
            {
                float pulse = 0.82f + 0.12f * MathF.Sin(i / (float)placeholder_frames * MathF.PI * 2);
                frames[i] = renderer.CreateTexture(
                    (int)placeholder_size,
                    (int)placeholder_size,
                    initialisationColour: new Color4(baseColour.R * pulse, baseColour.G * pulse, baseColour.B * pulse, 1f));
            }

            return frames;
        }

        private static Color4 placeholderColour(string clipName) => clipName switch
        {
            "hover" => new Color4(1f, 0.86f, 0.3f, 1f),
            "poke" => new Color4(1f, 0.55f, 0.16f, 1f),
            "starEasy" => new Color4(0.3f, 0.8f, 0.45f, 1f),
            "starHard" => new Color4(0.86f, 0.27f, 0.27f, 1f),
            "enter" => new Color4(0.3f, 0.78f, 0.86f, 1f),
            "combo50" => new Color4(0.3f, 0.55f, 1f, 1f),
            "combo300" => new Color4(0.7f, 0.3f, 1f, 1f),
            "miss" => new Color4(0.63f, 0.16f, 0.16f, 1f),
            "idlePlay" => new Color4(0.78f, 0.78f, 0.63f, 1f),
            "idleYawn" => new Color4(0.55f, 0.55f, 0.7f, 1f),
            "idleSleep" => new Color4(0.27f, 0.27f, 0.43f, 1f),
            _ => new Color4(0.7f, 0.7f, 0.75f, 1f),
        };

        private void onClipChanged(string _, string clip)
        {
            animation.ClearFrames();

            if (!clipTextures.TryGetValue(clip, out var frames) || frames.Length == 0)
            {
                string idleClip = currentPack?.Definition.DefaultState ?? "idle";
                if (!clipTextures.TryGetValue(idleClip, out frames) || frames.Length == 0)
                    return;
            }

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
        }

        private void applyBlendMode(EzPetClipDefinition? clipDef)
        {
            string? packMode = currentPack?.Definition.BlendMode;
            string? clipMode = clipDef?.BlendMode;
            animation.Blending = EzPetBlendModeExtensions.Resolve(packMode, clipMode).ToBlendingParameters();
        }

        private void checkClipFinished()
        {
            if (animation.Loop || animation.FrameCount == 0 || animation.Duration <= 0)
                return;

            if (animation.PlaybackPosition >= animation.Duration)
                stateMachine.NotifyClipFinished();
        }

        private void applyScale()
        {
            float s = (float)Math.Clamp(scale.Value, 0.25, 3.0);
            float width = placeholder_size * s;
            float height = placeholder_size * s;

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
            return input != null && input.CurrentState.Keyboard.Keys.IsPressed(osuTK.Input.Key.LAlt);
        }
    }
}
