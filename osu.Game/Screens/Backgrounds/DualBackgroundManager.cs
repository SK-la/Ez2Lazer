// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Backgrounds
{
    /// <summary>
    /// Manages dual background layers for Mania mode - one for the main background and one for the stage area.
    /// </summary>
    public partial class DualBackgroundManager : BackgroundScreen
    {
        private BackgroundScreenBeatmap.DimmableBackground mainBackground;
        private BackgroundScreenBeatmapMania.DimmableBackground maniaBackground;
        private object maniaPlayfield; // Using object to avoid circular dependency
        private WorkingBeatmap beatmap;

        [Resolved]
        private OsuConfigManager config { get; set; }

        private readonly Bindable<bool> enableManiaBackground = new Bindable<bool>();
        private readonly Bindable<double> maniaBackgroundDimLevel = new Bindable<double>();
        private readonly Bindable<double> maniaBackgroundBlurLevel = new Bindable<double>();
        private readonly Bindable<double> maniaBackgroundPositionX = new Bindable<double>();
        private readonly Bindable<double> maniaBackgroundPositionY = new Bindable<double>();
        private readonly Bindable<double> maniaBackgroundWidth = new Bindable<double>();
        private readonly Bindable<double> maniaBackgroundHeight = new Bindable<double>();

        public WorkingBeatmap Beatmap
        {
            get => beatmap;
            set
            {
                beatmap = value;
                if (IsLoaded)
                    LoadBackground();
            }
        }

        public object ManiaPlayfield
        {
            get => maniaPlayfield;
            set
            {
                maniaPlayfield = value;
                // Update masking when playfield changes
                if (maniaBackground != null)
                    updateManiaMasking();
            }
        }

        public DualBackgroundManager()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Create main background (always present)
            AddInternal(mainBackground = new BackgroundScreenBeatmap.DimmableBackground
            {
                RelativeSizeAxes = Axes.Both
            });

            // Create Mania background directly
            AddInternal(maniaBackground = new BackgroundScreenBeatmapMania.DimmableBackground
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 1 // Initially visible for immediate dual background effect
            });

            // Bind to configuration
            // enableManiaBackground.BindTo(config.GetBindable<bool>(OsuSetting.EnableManiaBackgroundLayer));
            // maniaBackgroundDimLevel.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundDimLevel));
            // maniaBackgroundBlurLevel.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundBlurLevel));
            // maniaBackgroundPositionX.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundPositionX));
            // maniaBackgroundPositionY.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundPositionY));
            // maniaBackgroundWidth.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundWidth));
            // maniaBackgroundHeight.BindTo(config.GetBindable<double>(OsuSetting.ManiaBackgroundHeight));

            enableManiaBackground.ValueChanged += onManiaBackgroundToggled;
            maniaBackgroundDimLevel.ValueChanged += onManiaBackgroundSettingsChanged;
            maniaBackgroundBlurLevel.ValueChanged += onManiaBackgroundSettingsChanged;
            maniaBackgroundPositionX.ValueChanged += onManiaBackgroundPositionChanged;
            maniaBackgroundPositionY.ValueChanged += onManiaBackgroundPositionChanged;
            maniaBackgroundWidth.ValueChanged += onManiaBackgroundPositionChanged;
            maniaBackgroundHeight.ValueChanged += onManiaBackgroundPositionChanged;

            // Load background if beatmap is already set
            if (beatmap != null)
                LoadBackground();
        }

        private void LoadBackground()
        {
            if (beatmap == null)
            {
                Logger.Log("DualBackgroundManager: Beatmap is null, cannot load background");
                return;
            }

            Logger.Log($"DualBackgroundManager: Loading background with file: {beatmap.Metadata.BackgroundFile}");

            // Force Mania background to be visible and log its state
            maniaBackground.Alpha = 1.0f;
            maniaBackground.AlwaysPresent = true;

            // Apply visual effects based on user settings
            maniaBackground.BlurAmount.Value = (float)maniaBackgroundBlurLevel.Value * 20.0f; // Scale to reasonable blur range
            maniaBackground.IgnoreUserSettings.Value = true; // Use our custom settings instead of global ones

            // Apply dim level using Colour property
            float dimFactor = 1.0f - (float)maniaBackgroundDimLevel.Value;
            maniaBackground.Colour = new Color4(dimFactor, dimFactor, dimFactor, 1.0f);

            Logger.Log(
                $"DualBackgroundManager: Mania background forced visible - Alpha: {maniaBackground.Alpha}, AlwaysPresent: {maniaBackground.AlwaysPresent}, Blur: {maniaBackground.BlurAmount.Value}, Dim: {maniaBackgroundDimLevel.Value:F2}, Colour: {maniaBackground.Colour}");

            // Update masking
            updateManiaMasking();
        }

        private void updateManiaMasking()
        {
            if (maniaPlayfield == null)
            {
                Logger.Log("DualBackgroundManager: ManiaPlayfield is null, cannot update masking");
                return;
            }

            try
            {
                // Use reflection to access ManiaPlayfield properties
                var playfieldType = maniaPlayfield.GetType();
                var skinnableComponentProperty = playfieldType.GetProperty("SkinnableComponentScreenSpaceDrawQuad");

                if (skinnableComponentProperty != null)
                {
                    // Calculate the stage area bounds
                    var stageBounds = (osu.Framework.Graphics.Primitives.Quad)skinnableComponentProperty.GetValue(maniaPlayfield);
                    Logger.Log($"DualBackgroundManager: Raw stage bounds - TopLeft: {stageBounds.TopLeft}, BottomRight: {stageBounds.BottomRight}, Size: {stageBounds.Size}");

                    // Convert screen space bounds to local space
                    var localBounds = ToLocalSpace(stageBounds);
                    Logger.Log($"DualBackgroundManager: Local bounds - TopLeft: {localBounds.TopLeft}, BottomRight: {localBounds.BottomRight}, Size: {localBounds.Size}");

                    // Update the Mania background to only show the stage area
                    if (localBounds.Size.X > 0 && localBounds.Size.Y > 0)
                    {
                        // Use actual stage bounds
                        maniaBackground.Position = localBounds.TopLeft;
                        maniaBackground.Size = localBounds.Size;
                        maniaBackground.RelativeSizeAxes = Axes.None; // Use absolute positioning
                        Logger.Log(
                            $"DualBackgroundManager: Mania masking updated - Position: {maniaBackground.Position}, Size: {maniaBackground.Size}, RelativeSizeAxes: {maniaBackground.RelativeSizeAxes}");
                    }
                    else
                    {
                        // Try to get the playfield's actual size and position
                        var drawableProperty = playfieldType.GetProperty("DrawSize");
                        var positionProperty = playfieldType.GetProperty("Position");

                        if (drawableProperty != null && positionProperty != null)
                        {
                            var drawSize = (Vector2)drawableProperty.GetValue(maniaPlayfield);
                            var position = (Vector2)positionProperty.GetValue(maniaPlayfield);

                            Logger.Log($"DualBackgroundManager: Playfield DrawSize: {drawSize}, Position: {position}");

                            if (drawSize.X > 0 && drawSize.Y > 0)
                            {
                                // Check if playfield size is reasonable (not full screen)
                                if (drawSize.X < 1000 && drawSize.Y < 600) // Reasonable stage area size
                                {
                                    // Use playfield size and position
                                    maniaBackground.Position = position;
                                    maniaBackground.Size = drawSize;
                                    maniaBackground.RelativeSizeAxes = Axes.None;
                                    Logger.Log($"DualBackgroundManager: Using playfield bounds - Position: {maniaBackground.Position}, Size: {maniaBackground.Size}");
                                }
                                else
                                {
                                    // Playfield size is too large (full screen), use slider settings
                                    Logger.Log($"DualBackgroundManager: Playfield size too large ({drawSize}), using slider settings");
                                    maniaBackground.Position = new Vector2((float)maniaBackgroundPositionX.Value, (float)maniaBackgroundPositionY.Value);
                                    maniaBackground.Size = new Vector2((float)maniaBackgroundWidth.Value, (float)maniaBackgroundHeight.Value);
                                    maniaBackground.RelativeSizeAxes = Axes.Both;
                                    maniaBackground.RelativePositionAxes = Axes.Both;
                                    Logger.Log(
                                        $"DualBackgroundManager: Using slider settings - Position: {maniaBackground.Position}, Size: {maniaBackground.Size}, RelativeSizeAxes: {maniaBackground.RelativeSizeAxes}, RelativePositionAxes: {maniaBackground.RelativePositionAxes}");
                                }
                            }
                            else
                            {
                                // Use slider settings when playfield size is zero
                                Logger.Log("DualBackgroundManager: Playfield size is zero, using slider settings");
                                maniaBackground.Position = new Vector2((float)maniaBackgroundPositionX.Value, (float)maniaBackgroundPositionY.Value);
                                maniaBackground.Size = new Vector2((float)maniaBackgroundWidth.Value, (float)maniaBackgroundHeight.Value);
                                maniaBackground.RelativeSizeAxes = Axes.Both;
                                maniaBackground.RelativePositionAxes = Axes.Both;
                                Logger.Log(
                                    $"DualBackgroundManager: Using slider settings - Position: {maniaBackground.Position}, Size: {maniaBackground.Size}, RelativeSizeAxes: {maniaBackground.RelativeSizeAxes}, RelativePositionAxes: {maniaBackground.RelativePositionAxes}");
                            }
                        }
                        else
                        {
                            // Use slider settings when properties are not found
                            Logger.Log("DualBackgroundManager: Properties not found, using slider settings");
                            maniaBackground.Position = new Vector2((float)maniaBackgroundPositionX.Value, (float)maniaBackgroundPositionY.Value);
                            maniaBackground.Size = new Vector2((float)maniaBackgroundWidth.Value, (float)maniaBackgroundHeight.Value);
                            maniaBackground.RelativeSizeAxes = Axes.Both;
                            maniaBackground.RelativePositionAxes = Axes.Both;
                            Logger.Log(
                                $"DualBackgroundManager: Using slider settings - Position: {maniaBackground.Position}, Size: {maniaBackground.Size}, RelativeSizeAxes: {maniaBackground.RelativeSizeAxes}, RelativePositionAxes: {maniaBackground.RelativePositionAxes}");
                        }
                    }
                }
                else
                    Logger.Log("DualBackgroundManager: SkinnableComponentScreenSpaceDrawQuad property not found");
            }
            catch (Exception ex)
            {
                // If reflection fails, disable masking
                maniaBackground.Masking = false;
                Logger.Log($"DualBackgroundManager: Masking update failed: {ex.Message}");
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Set initial state
            onManiaBackgroundToggled(new ValueChangedEvent<bool>(false, enableManiaBackground.Value));
        }

        private void onManiaBackgroundToggled(ValueChangedEvent<bool> e)
        {
            if (maniaBackground == null) return;

            if (e.NewValue)
            {
                maniaBackground.FadeIn(500, Easing.OutQuint);
                updateManiaMasking();
            }
            else
                maniaBackground.FadeOut(500, Easing.OutQuint);
        }

        private void onManiaBackgroundSettingsChanged(ValueChangedEvent<double> e)
        {
            if (maniaBackground == null) return;

            // Update blur amount
            maniaBackground.BlurAmount.Value = (float)maniaBackgroundBlurLevel.Value * 20.0f;

            // Update dim level using Colour property for better visibility
            float dimFactor = 1.0f - (float)maniaBackgroundDimLevel.Value;
            maniaBackground.Colour = new Color4(dimFactor, dimFactor, dimFactor, 1.0f);

            Logger.Log($"DualBackgroundManager: Settings updated - Blur: {maniaBackground.BlurAmount.Value}, Dim: {maniaBackgroundDimLevel.Value:F2}, Colour: {maniaBackground.Colour}");
        }

        private void onManiaBackgroundPositionChanged(ValueChangedEvent<double> e)
        {
            if (maniaBackground == null) return;

            // Update position and size based on slider values
            // When using RelativeSizeAxes = Axes.Both, Position also needs to be relative
            maniaBackground.Position = new Vector2((float)maniaBackgroundPositionX.Value, (float)maniaBackgroundPositionY.Value);
            maniaBackground.Size = new Vector2((float)maniaBackgroundWidth.Value, (float)maniaBackgroundHeight.Value);
            maniaBackground.RelativeSizeAxes = Axes.Both;
            maniaBackground.RelativePositionAxes = Axes.Both; // Enable relative positioning

            Logger.Log(
                $"DualBackgroundManager: Position updated - X: {maniaBackgroundPositionX.Value:F2}, Y: {maniaBackgroundPositionY.Value:F2}, Width: {maniaBackgroundWidth.Value:F2}, Height: {maniaBackgroundHeight.Value:F2}");
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (enableManiaBackground != null)
                enableManiaBackground.ValueChanged -= onManiaBackgroundToggled;
            if (maniaBackgroundDimLevel != null)
                maniaBackgroundDimLevel.ValueChanged -= onManiaBackgroundSettingsChanged;
            if (maniaBackgroundBlurLevel != null)
                maniaBackgroundBlurLevel.ValueChanged -= onManiaBackgroundSettingsChanged;
            if (maniaBackgroundPositionX != null)
                maniaBackgroundPositionX.ValueChanged -= onManiaBackgroundPositionChanged;
            if (maniaBackgroundPositionY != null)
                maniaBackgroundPositionY.ValueChanged -= onManiaBackgroundPositionChanged;
            if (maniaBackgroundWidth != null)
                maniaBackgroundWidth.ValueChanged -= onManiaBackgroundPositionChanged;
            if (maniaBackgroundHeight != null)
                maniaBackgroundHeight.ValueChanged -= onManiaBackgroundPositionChanged;
        }
    }
}
