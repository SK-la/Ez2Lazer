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
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.HUD;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
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

        private static readonly Dictionary<bool, (Color4 Color, string TopText, string BottomText)> position_mode_config = new Dictionary<bool, (Color4 Color, string TopText, string BottomText)>
        {
            [true] = (new Color4(0.2f, 0.4f, 0.8f, 0.3f), EzLocalizationManager.SwitchToAbsolute, EzLocalizationManager.SwitchToAbsolute),
            [false] = (new Color4(0.8f, 0.2f, 0.4f, 0.3f), EzLocalizationManager.SwitchToRelative, EzLocalizationManager.SwitchToRelative)
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
                        LabelText = EzLocalizationManager.GlobalTextureName,
                        TooltipText = EzLocalizationManager.GlobalTextureNameTooltip,
                        Current = nameOfGameTheme,
                    },
                    new SettingsDropdown<string>
                    {
                        LabelText = EzLocalizationManager.StageSet,
                        TooltipText = EzLocalizationManager.StageSetTooltip,
                        Current = nameOfStage,
                        Items = availableStageSets,
                    },
                    new SettingsDropdown<string>
                    {
                        LabelText = EzLocalizationManager.NoteSet,
                        TooltipText = EzLocalizationManager.NoteSetTooltip,
                        Current = nameOfNote,
                        Items = availableNoteSets,
                    },
                    new SettingsEnumDropdown<ColumnWidthStyle>
                    {
                        LabelText = EzLocalizationManager.ColumnWidthStyle,
                        TooltipText = EzLocalizationManager.ColumnWidthStyleTooltip,
                        Current = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle),
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.ColumnWidth,
                        TooltipText = EzLocalizationManager.ColumnWidthTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth),
                        KeyboardStep = 1.0f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.SpecialFactor,
                        TooltipText = EzLocalizationManager.SpecialFactorTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsCheckbox
                    {
                        LabelText = EzLocalizationManager.GlobalHitPosition,
                        TooltipText = EzLocalizationManager.GlobalHitPositionTooltip,
                        Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.GlobalHitPosition),
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.HitPosition,
                        TooltipText = EzLocalizationManager.HitPositionTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition),
                        KeyboardStep = 1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.HitTargetFloatFixed,
                        TooltipText = EzLocalizationManager.HitTargetFloatFixedTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.HitTargetAlpha,
                        TooltipText = EzLocalizationManager.HitTargetAlphaTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                        KeyboardStep = 0.01f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.NoteHeightScale,
                        TooltipText = EzLocalizationManager.NoteHeightScaleTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.ManiaHoldTailMaskGradientHeight,
                        TooltipText = EzLocalizationManager.ManiaHoldTailMaskGradientHeightTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight),
                        KeyboardStep = 1.0f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.ManiaHoldTailAlpha,
                        TooltipText = EzLocalizationManager.ManiaHoldTailAlphaTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha),
                        KeyboardStep = 0.1f,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.NoteTrackLine,
                        TooltipText = EzLocalizationManager.NoteTrackLineTooltip,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
                    },
                    refreshSkinButton = new SettingsButton
                    {
                        Action = refreshSkin,
                        Text = EzLocalizationManager.RefreshSaveSkin,
                        TooltipText = EzLocalizationManager.RefreshSaveSkinTooltip
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
                Logger.Log($"Unknown resource type: {type}", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            targetList.Clear();

            if (!resource_paths.TryGetValue(type, out string? relativePath))
            {
                Logger.Log($"Unknown resource type: {type}", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            try
            {
                string? dataFolderPath = storage.GetFullPath(relativePath);
                // Debug.Assert(!string.IsNullOrEmpty(dataFolderPath));

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzSkinTab create {type} Path: {dataFolderPath}");
                }

                string[] directories = Directory.GetDirectories(dataFolderPath);
                targetList.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);

                Logger.Log($"Found {targetList.Count} {type} sets in {dataFolderPath}", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"EzSkinTab Load {type} FolderSets Error");
            }
        }
    }
}
