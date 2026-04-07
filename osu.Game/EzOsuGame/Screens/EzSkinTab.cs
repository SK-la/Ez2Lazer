// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens
{
    public partial class EzSkinTab : EditorSidebarSection
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinTab()
            : base("EZ Skin Settings") { }

        private static readonly Dictionary<string, string> resource_paths = new Dictionary<string, string>
        {
            ["note"] = Path.Combine("EzResources", "note"),
            ["Stage"] = Path.Combine("EzResources", "Stage"),
            ["GameTheme"] = Path.Combine("EzResources", "GameTheme")
        };

        private static readonly Dictionary<bool, (Color4 Color, LocalisableString TopText, LocalisableString BottomText)> position_mode_config =
            new Dictionary<bool, (Color4 Color, LocalisableString TopText, LocalisableString BottomText)>
            {
                [true] = (new Color4(0.2f, 0.4f, 0.8f, 0.3f), EzSkinStrings.SWITCH_TO_ABSOLUTE, EzSkinStrings.SWITCH_TO_ABSOLUTE),
                [false] = (new Color4(0.8f, 0.2f, 0.4f, 0.3f), EzSkinStrings.SWITCH_TO_RELATIVE, EzSkinStrings.SWITCH_TO_RELATIVE)
            };

        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();
        private Bindable<string> nameOfNote = null!;
        private Bindable<string> nameOfStage = null!;
        private Bindable<EzEnumGameThemeName> nameOfGameTheme = null!;

        private SettingsButton refreshSkinButton = null!;
        private bool isAbsolutePosition = true;

        [BackgroundDependencyLoader]
        private void load()
        {
            loadFolderSets("note");
            loadFolderSets("Stage");
            // loadFolderSets("GameTheme");

            nameOfNote = ezSkinConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            nameOfStage = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);
            nameOfGameTheme = ezSkinConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new SettingsEnumDropdown<EzEnumGameThemeName>
                    {
                        LabelText = EzSkinStrings.GLOBAL_TEXTURE_NAME,
                        TooltipText = EzSkinStrings.GLOBAL_TEXTURE_NAME_TOOLTIP,
                        Current = nameOfGameTheme,
                    },
                    new SettingsDropdown<string>
                    {
                        LabelText = EzSkinStrings.STAGE_SET,
                        TooltipText = EzSkinStrings.STAGE_SET_TOOLTIP,
                        Current = nameOfStage,
                        Items = availableStageSets,
                    },
                    new SettingsDropdown<string>
                    {
                        LabelText = EzSkinStrings.NOTE_SET,
                        TooltipText = EzSkinStrings.NOTE_SET_TOOLTIP,
                        Current = nameOfNote,
                        Items = availableNoteSets,
                    },
                    new SettingsEnumDropdown<ColumnWidthStyle>
                    {
                        LabelText = EzSkinStrings.COLUMN_WIDTH_STYLE,
                        TooltipText = EzSkinStrings.COLUMN_WIDTH_STYLE_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle),
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.COLUMN_WIDTH,
                        TooltipText = EzSkinStrings.COLUMN_WIDTH_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth),
                        KeyboardStep = 1.0f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.SPECIAL_FACTOR,
                        TooltipText = EzSkinStrings.SPECIAL_FACTOR_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsCheckbox
                    {
                        LabelText = EzSkinStrings.GLOBAL_HIT_POSITION,
                        TooltipText = EzSkinStrings.GLOBAL_HIT_POSITION_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable),
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.HIT_POSITION,
                        TooltipText = EzSkinStrings.HIT_POSITION_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition),
                        KeyboardStep = 1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED,
                        TooltipText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.HIT_TARGET_ALPHA,
                        TooltipText = EzSkinStrings.HIT_TARGET_ALPHA_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                        KeyboardStep = 0.01f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.NOTE_HEIGHT_SCALE,
                        TooltipText = EzSkinStrings.NOTE_HEIGHT_SCALE_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.NOTE_CORNER_RADIUS,
                        TooltipText = EzSkinStrings.NOTE_CORNER_RADIUS_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteCornerRadius),
                        KeyboardStep = 1.0f,
                    },
                    new SettingsCheckbox
                    {
                        LabelText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE,
                        TooltipText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.ManiaLNGradientEnable),
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.LN_TAIL_MASK_GRADIENT_HEIGHT,
                        TooltipText = EzSkinStrings.LN_TAIL_MASK_GRADIENT_HEIGHT_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight),
                        KeyboardStep = 1.0f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.LN_TAIL_ALPHA,
                        TooltipText = EzSkinStrings.LN_TAIL_ALPHA_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.NOTE_TRACK_LINE,
                        TooltipText = EzSkinStrings.NOTE_TRACK_LINE_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
                    },
                    refreshSkinButton = new SettingsButton
                    {
                        Action = refreshSkin,
                        Text = EzSkinStrings.REFRESH_SAVE_SKIN,
                        TooltipText = EzSkinStrings.REFRESH_SAVE_SKIN_TOOLTIP
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            nameOfNote.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.NoteSetName, e.NewValue));
            nameOfStage.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.StageName, e.NewValue));
            nameOfGameTheme.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.GameThemeName, e.NewValue));
            nameOfGameTheme.BindValueChanged(e => updateAllEzTextureNames(e.NewValue));
        }

        #region Save按钮处理

        private void refreshSkin()
        {
            isAbsolutePosition = !isAbsolutePosition;
            skinManager.CurrentSkinInfo.TriggerChange();

            updateButtonAppearance();
        }

        // 切换按钮外观以反映当前的定位模式, 可能不稳定，目前视为已废弃功能，通过按钮提示告知尽量不使用
        private void updateButtonAppearance()
        {
            var config = position_mode_config[isAbsolutePosition];

            var box = refreshSkinButton.ChildrenOfType<Box>().FirstOrDefault();
            box?.FadeColour(config.Color, 200);

            var textContainer = refreshSkinButton.ChildrenOfType<FillFlowContainer>().FirstOrDefault();
            var texts = textContainer?.ChildrenOfType<OsuSpriteText>().ToArray();

            if (texts?.Length >= 2)
            {
                texts[0].Text = config.TopText;
                texts[1].Text = config.BottomText;
            }
        }

        #endregion

        #region 刷新所有EzComponent的纹理名称

        private void updateAllEzTextureNames(EzEnumGameThemeName textureGameTheme)
        {
            var scoreTexts = this.ChildrenOfType<EzScoreText>();
            var comboTexts = this.ChildrenOfType<EzComboText>();
            var hitResultScores = this.ChildrenOfType<EzComHitResultScore>();

            foreach (var scoreText in scoreTexts)
                scoreText.FontName.Value = textureGameTheme;

            foreach (var comboText in comboTexts)
                comboText.FontName.Value = textureGameTheme;

            foreach (var hitResultScore in hitResultScores)
                hitResultScore.NameDropdown.Value = textureGameTheme;
        }

        #endregion

        private void loadFolderSets(string type)
        {
            List<string> targetList;

            if (type.Equals("note", StringComparison.OrdinalIgnoreCase))
                targetList = availableNoteSets;
            else if (type.Equals("Stage", StringComparison.OrdinalIgnoreCase))
                targetList = availableStageSets;
            // else if (type.Equals("GameTheme", StringComparison.OrdinalIgnoreCase))
            //     targetList = availableGameThemes;
            else
            {
                Logger.Log($"Unknown resource type: {type}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return;
            }

            targetList.Clear();

            if (!resource_paths.TryGetValue(type, out string? relativePath))
            {
                Logger.Log($"Unknown resource type: {type}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return;
            }

            try
            {
                string? dataFolderPath = storage.GetFullPath(relativePath);
                // Debug.Assert(!string.IsNullOrEmpty(dataFolderPath));

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzSkinTab create {type} Path: {dataFolderPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                }

                string[] directories = Directory.GetDirectories(dataFolderPath);
                targetList.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);

                Logger.Log($"Found {targetList.Count} {type} sets in {dataFolderPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"EzSkinTab Load {type} FolderSets Error");
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                nameOfNote.UnbindAll();
                nameOfStage.UnbindAll();
                nameOfGameTheme.UnbindAll();

                refreshSkinButton.Action = null;

                availableNoteSets.Clear();
                availableStageSets.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
