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
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class EzSkinSettings : EditorSidebarSection
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinSettings()
            : base("EZ Skin Settings") { }

        private static readonly Dictionary<string, string> resource_paths = new Dictionary<string, string>
        {
            ["note"] = Path.Combine("EzResources", "note"),
            ["Stage"] = Path.Combine("EzResources", "Stage"),
            ["GameTheme"] = Path.Combine("EzResources", "GameTheme")
        };

        private static readonly Dictionary<bool, (Color4 Color, string TopText, string BottomText)> position_mode_config = new Dictionary<bool, (Color4 Color, string TopText, string BottomText)>
        {
            [true] = (new Color4(0.2f, 0.4f, 0.8f, 0.3f), "SwitchToAbsolute".Localize(), "SwitchToAbsolute".Localize()),
            [false] = (new Color4(0.8f, 0.2f, 0.4f, 0.3f), "SwitchToRelative".Localize(), "SwitchToRelative".Localize())
        };

        // TODO: 优化为动态枚举, 不能用Bindable<string>，这不是列表/枚举，会导致控件下拉栏无选项
        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();
        private Bindable<string> nameOfNote = new Bindable<string>();
        private Bindable<string> nameOfStage = new Bindable<string>();
        private Bindable<EzEnumGameThemeName> nameOfGameTheme = new Bindable<EzEnumGameThemeName>();

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
            // setDefaultSelection(nameOfGameTheme, availableGameThemes);
            createUI();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            nameOfNote.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.NoteSetName, e.NewValue));
            nameOfStage.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.StageName, e.NewValue));
            nameOfGameTheme.BindValueChanged(e => ezSkinConfig.SetValue(Ez2Setting.GameThemeName, e.NewValue));
            nameOfGameTheme.BindValueChanged(e => updateAllEzTextureNames(e.NewValue));
        }

        private void setDefaultSelection(Bindable<string> bindable, List<string> availableItems)
        {
            if (availableItems.Count > 1 || !availableItems.Contains(bindable.Value))
            {
                bindable.Value = availableItems[1];
            }
        }

        private void createUI()
        {
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        new SettingsEnumDropdown<EzEnumGameThemeName>
                        {
                            LabelText = "GlobalTextureName".Localize(),
                            TooltipText = "GlobalTextureNameTooltip".Localize(),
                            Current = nameOfGameTheme,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "StageSet".Localize(),
                            TooltipText = "StageSetTooltip".Localize(),
                            Current = nameOfStage,
                            Items = availableStageSets,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "NoteSet".Localize(),
                            TooltipText = "NoteSetTooltip".Localize(),
                            Current = nameOfNote,
                            Items = availableNoteSets,
                        },
                        new SettingsEnumDropdown<EzColumnWidthStyle>
                        {
                            LabelText = "ColumnWidthStyle".Localize(),
                            TooltipText = "ColumnWidthStyleTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<EzColumnWidthStyle>(Ez2Setting.ColumnWidthStyle),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "ColumnWidth".Localize(),
                            TooltipText = "ColumnWidthTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth),
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "SpecialFactor".Localize(),
                            TooltipText = "SpecialFactorTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsCheckbox
                        {
                            LabelText = "GlobalHitPosition".Localize(),
                            TooltipText = "GlobalHitPositionTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.GlobalHitPosition),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitPosition".Localize(),
                            TooltipText = "HitPositionTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition),
                            KeyboardStep = 1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitTargetFloatFixed".Localize(),
                            TooltipText = "HitTargetFloatFixedTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitTargetAlpha".Localize(),
                            TooltipText = "HitTargetAlphaTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                            KeyboardStep = 0.01f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "NoteHeightScale".Localize(),
                            TooltipText = "NoteHeightScaleTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "ManiaHoldTailMaskGradientHeight".Localize(),
                            TooltipText = "ManiaHoldTailMaskGradientHeightTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight),
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "ManiaHoldTailAlpha".Localize(),
                            TooltipText = "ManiaHoldTailAlphaTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "NoteTrackLine".Localize(),
                            TooltipText = "NoteTrackLineTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
                        },
                        refreshSkinButton = new SettingsButton
                        {
                            Action = refreshSkin,
                            Text = "RefreshSaveSkin".Localize(),
                            TooltipText = "RefreshSaveSkin".Localize()
                        }
                    }
                }
            };
        }

        #region Save按钮处理

        private void refreshSkin()
        {
            isAbsolutePosition = !isAbsolutePosition;
            skinManager.CurrentSkinInfo.TriggerChange();
            // skinManager.Save(skinManager.CurrentSkin.Value);
            updateButtonAppearance();
        }

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
            var root = findRootDrawable();

            var scoreTexts = root.ChildrenOfType<EzScoreText>();
            var comboTexts = root.ChildrenOfType<EzComboText>();
            var hitResultScores = root.ChildrenOfType<EzComHitResultScore>();

            foreach (var scoreText in scoreTexts)
                scoreText.FontName.Value = textureGameTheme;

            foreach (var comboText in comboTexts)
                comboText.FontName.Value = textureGameTheme;

            foreach (var hitResultScore in hitResultScores)
                hitResultScore.NameDropdown.Value = textureGameTheme;
        }

        private Drawable findRootDrawable()
        {
            var root = this as Drawable;
            while (root.Parent != null)
                root = root.Parent;
            return root;
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

    #region 拓展按钮的多行文本显示

    public static class SettingsButtonExtensions
    {
        public static SettingsButton WithTwoLineText(this SettingsButton button, string topText, string bottomText, int fontSize = 14)
        {
            button.Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.AliceBlue,
                        Alpha = 0.1f
                    },
                    // 文本层
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = topText,
                                Font = OsuFont.GetFont(size: fontSize),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre
                            },
                            new OsuSpriteText
                            {
                                Text = bottomText,
                                Font = OsuFont.GetFont(size: fontSize),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre
                            }
                        }
                    }
                }
            };

            return button;
        }
    }

    #endregion
}
