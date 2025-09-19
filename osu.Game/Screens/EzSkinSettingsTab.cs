// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens
{
    public partial class EzSkinSettings : EditorSidebarSection
    {
        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinSettings()
            : base("EZ Skin Settings") { }

        private static readonly Dictionary<string, string> resource_paths = new Dictionary<string, string>
        {
            ["note"] = @"EzResources\note",
            ["Stage"] = @"EzResources\Stage"
        };

        private static readonly Dictionary<bool, (Color4 Color, string TopText, string BottomText)> position_mode_config = new Dictionary<bool, (Color4 Color, string TopText, string BottomText)>
        {
            [true] = (new Color4(0.2f, 0.4f, 0.8f, 0.3f), "SwitchToAbsolute".Localize(), "SwitchToAbsolute".Localize()),
            [false] = (new Color4(0.8f, 0.2f, 0.4f, 0.3f), "SwitchToRelative".Localize(), "SwitchToRelative".Localize())
        };

        private readonly Bindable<string> selectedNoteSet = new Bindable<string>();
        private readonly Bindable<string> selectedStageSet = new Bindable<string>();
        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();

        private readonly Bindable<EzSelectorNameSet> globalTextureName = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)4);

        private SettingsButton refreshSkinButton = null!;
        private bool isAbsolutePosition = true;

        [BackgroundDependencyLoader]
        private void load()
        {
            globalTextureName.Value = (EzSelectorNameSet)ezSkinConfig.GetBindable<int>(EzSkinSetting.GlobalTextureName).Value;

            loadFolderSets("note");
            loadFolderSets("Stage");

            setDefaultSelection(selectedNoteSet, availableNoteSets, ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName));
            setDefaultSelection(selectedStageSet, availableStageSets, ezSkinConfig.Get<string>(EzSkinSetting.StageName));
            createUI();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            globalTextureName.BindValueChanged(e => updateAllEzTextureNames(e.NewValue));
            selectedNoteSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.NoteSetName, e.NewValue));
            selectedStageSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.StageName, e.NewValue));
        }

        private void setDefaultSelection(Bindable<string> bindable, List<string> availableItems, string configuredValue)
        {
            if (!string.IsNullOrEmpty(configuredValue) && availableItems.Contains(configuredValue))
            {
                bindable.Value = configuredValue;
            }
            else if (availableItems.Count > 1)
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
                        new SettingsEnumDropdown<EzSelectorNameSet>
                        {
                            LabelText = "GlobalTextureName".Localize(),
                            TooltipText = "GlobalTextureNameTooltip".Localize(),
                            Current = globalTextureName,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "StageSet".Localize(),
                            TooltipText = "StageSetTooltip".Localize(),
                            Current = selectedStageSet,
                            Items = availableStageSets,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "NoteSet".Localize(),
                            TooltipText = "NoteSetTooltip".Localize(),
                            Current = selectedNoteSet,
                            Items = availableNoteSets,
                        },
                        new SettingsEnumDropdown<EzColumnWidthStyle>
                        {
                            LabelText = "ColumnWidthStyle".Localize(),
                            TooltipText = "ColumnWidthStyleTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<EzColumnWidthStyle>(EzSkinSetting.ColumnWidthStyle),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "ColumnWidth".Localize(),
                            TooltipText = "ColumnWidthTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth),
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "SpecialFactor".Localize(),
                            TooltipText = "SpecialFactorTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsCheckbox
                        {
                            LabelText = "GlobalHitPosition".Localize(),
                            TooltipText = "GlobalHitPositionTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<bool>(EzSkinSetting.GlobalHitPosition),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitPosition".Localize(),
                            TooltipText = "HitPositionTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition),
                            KeyboardStep = 1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitTargetFloatFixed".Localize(),
                            TooltipText = "HitTargetFloatFixedTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitTargetFloatFixed),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "HitTargetAlpha".Localize(),
                            TooltipText = "HitTargetAlphaTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitTargetAlpha),
                            KeyboardStep = 0.01f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "NoteHeightScale".Localize(),
                            TooltipText = "NoteHeightScaleTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth),
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "NoteTrackLine".Localize(),
                            TooltipText = "NoteTrackLineTooltip".Localize(),
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteTrackLineHeight),
                        },
                        refreshSkinButton = new SettingsButton
                        {
                            Action = RefreshSkin,
                            Text = "RefreshSaveSkin".Localize(),
                            TooltipText = "RefreshSaveSkin".Localize()
                        }
                    }
                }
            };
        }

        #region Save按钮处理

        public void RefreshSkin()
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

        private void updateAllEzTextureNames(EzSelectorNameSet textureName)
        {
            var root = findRootDrawable();

            var scoreTexts = root.ChildrenOfType<EzScoreText>();
            var comboTexts = root.ChildrenOfType<EzComboText>();
            var hitResultScores = root.ChildrenOfType<EzComHitResultScore>();

            foreach (var scoreText in scoreTexts)
                scoreText.FontName.Value = textureName;

            foreach (var comboText in comboTexts)
                comboText.FontName.Value = textureName;

            foreach (var hitResultScore in hitResultScores)
                hitResultScore.NameDropdown.Value = textureName;
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
            List<string> targetList = type.Equals("note", StringComparison.OrdinalIgnoreCase) ? availableNoteSets : availableStageSets;
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
