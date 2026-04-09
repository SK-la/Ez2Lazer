// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Dialog;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    public enum ButtonStyle
    {
        Primary,
        Danger,
        Warning,
        Disabled,
    }

    public readonly record struct BranchManagerEntry(
        bool IsCollectionRow,
        string SelectionKey,
        Guid? SourceCollectionId,
        string SourceCollectionName,
        int SourceCollectionBeatmapCount,
        EzAnalysisPersistentStore.SongsBranchDescriptor? Branch,
        bool CollectionHiddenApplied)
    {
        public bool HasBranch => Branch.HasValue;

        public string DisplayName => HasBranch ? Branch!.Value.Metadata.DisplayName : SourceCollectionName;

        public string? BranchDatabasePath => HasBranch ? Branch!.Value.DatabasePath : null;

        public string RelativePath => HasBranch ? Branch!.Value.RelativePath : string.Empty;

        public string ModsDisplay => HasBranch ? Branch!.Value.Metadata.ModsDisplay : string.Empty;

        public int BeatmapCount => SourceCollectionBeatmapCount > 0 ? SourceCollectionBeatmapCount : Branch?.Metadata.BeatmapCount ?? 0;

        public bool HiddenApplied => IsCollectionRow && CollectionHiddenApplied;

        public EzAnalysisPersistentStore.SongsBranchDescriptor BranchValue => Branch!.Value;
    }

    public partial class EzBranchListItem : OsuClickableContainer
    {
        private const float item_height = 45;
        private const float button_width = item_height * 0.75f;

        private readonly BranchManagerEntry entry;

        private Box background = null!;
        private Drawable nameArea = null!;

        public bool IsSelected { get; init; }

        public bool IsActive { get; init; }

        public Action<BranchManagerEntry>? SelectAction { get; init; }

        public Action<BranchManagerEntry>? ToggleHideAction { get; init; }

        public Action<BranchManagerEntry>? ToggleActivationAction { get; init; }

        public Action<BranchManagerEntry>? DeleteBranchAction { get; init; }

        public Action<BranchManagerEntry>? DeleteCollectionBeatmapsAction { get; init; }

        public EzBranchListItem(BranchManagerEntry entry)
        {
            this.entry = entry;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = IsSelected ? colours.BlueDarker : colours.GreySeaFoam,
                        Alpha = IsSelected ? 0.9f : 0.35f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                        Colour = colours.Yellow,
                        Alpha = !entry.IsCollectionRow && IsActive ? 1 : 0,
                    },
                    createActionButtonContainer(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Vertical = 10, Left = 14, Right = button_width * 2 + item_height / 2 },
                        Children = new[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 6),
                                Children = new[]
                                {
                                    nameArea = new NameDisplayBox(entry.DisplayName)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = item_height,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = createDetailsText(),
                                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Medium),
                                        Colour = IsActive ? colours.Yellow : colours.BlueLight,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = createSourceText(),
                                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Medium),
                                        Colour = colours.Yellow,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = createPathText(),
                                        Font = OsuFont.Default.With(size: 12),
                                        Colour = colours.GreySeaFoamLighter,
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            SelectAction?.Invoke(entry);
            return true;
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(1, 100, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(IsSelected ? 0.9f : 0.35f, 100, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        private string createDetailsText()
        {
            if (!entry.HasBranch)
            {
                string hiddenText = entry.HiddenApplied ? $"{BranchListItemStrings.HIDDEN_BRANCH_BADGE} · " : string.Empty;
                return $"{hiddenText}{BranchListItemStrings.COLLECTION_PENDING_BADGE} · {entry.BeatmapCount:#,0} {BranchListItemStrings.BEATMAPS_UNIT}";
            }

            string activeText = IsActive ? $"{BranchListItemStrings.ACTIVE_BRANCH_BADGE} · " : string.Empty;
            return $"{activeText}{entry.ModsDisplay} · {entry.BeatmapCount:#,0} {BranchListItemStrings.BEATMAPS_UNIT}";
        }

        private LocalisableString createSourceText()
            => $"{BranchListItemStrings.SOURCE_COLLECTION_PREFIX}{getSourceCollectionDisplayName()}";

        private LocalisableString createPathText()
            => entry.HasBranch ? entry.RelativePath : BranchListItemStrings.GENERATE_HINT;

        private string getSourceCollectionDisplayName()
            => string.IsNullOrWhiteSpace(entry.SourceCollectionName)
                ? BranchListItemStrings.UNKNOWN_COLLECTION
                : entry.SourceCollectionName;

        private Drawable createActionButtonContainer()
        {
            if (entry.IsCollectionRow)
            {
                return new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Direction = FillDirection.Horizontal,
                    RelativeSizeAxes = Axes.Y,
                    Width = button_width * 2 + item_height / 4,
                    Spacing = new Vector2(2, 0),
                    Children = new Drawable[]
                    {
                        new RowActionButton(entry.HiddenApplied ? FontAwesome.Solid.Eye : FontAwesome.Solid.EyeSlash,
                            entry.HiddenApplied ? BranchListItemStrings.RESTORE_BRANCH_HIDE_TOOLTIP : BranchListItemStrings.APPLY_BRANCH_HIDE_TOOLTIP,
                            entry.HiddenApplied ? ButtonStyle.Primary : ButtonStyle.Warning)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            IsTextBoxHovered = v => nameArea.ReceivePositionalInputAt(v),
                            Action = () =>
                            {
                                SelectAction?.Invoke(entry);
                                ToggleHideAction?.Invoke(entry);
                            },
                        },
                        new RowActionButton(FontAwesome.Solid.Trash, BranchListItemStrings.DELETE_COLLECTION_LOCAL_BEATMAPS_TOOLTIP, ButtonStyle.Danger)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            IsTextBoxHovered = v => nameArea.ReceivePositionalInputAt(v),
                            Action = () =>
                            {
                                SelectAction?.Invoke(entry);
                                DeleteCollectionBeatmapsAction?.Invoke(entry);
                            },
                        }
                    },
                };
            }

            if (!entry.HasBranch)
                return new Container();

            return new FillFlowContainer
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Direction = FillDirection.Horizontal,
                RelativeSizeAxes = Axes.Y,
                Width = button_width * 2 + item_height / 4,
                Spacing = new Vector2(2, 0),
                Children = new Drawable[]
                {
                    new RowActionButton(IsActive ? FontAwesome.Solid.PowerOff : FontAwesome.Solid.Play,
                        IsActive ? BranchListItemStrings.DEACTIVATE_BRANCH_TOOLTIP : BranchListItemStrings.ACTIVATE_BRANCH_TOOLTIP,
                        IsActive ? ButtonStyle.Danger : ButtonStyle.Primary)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.5f,
                        IsTextBoxHovered = v => nameArea.ReceivePositionalInputAt(v),
                        Action = () =>
                        {
                            SelectAction?.Invoke(entry);
                            ToggleActivationAction?.Invoke(entry);
                        },
                    },
                    new RowActionButton(FontAwesome.Solid.Trash, BranchListItemStrings.DELETE_BRANCH_TOOLTIP, ButtonStyle.Danger)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.5f,
                        IsTextBoxHovered = v => nameArea.ReceivePositionalInputAt(v),
                        Action = () =>
                        {
                            SelectAction?.Invoke(entry);
                            DeleteBranchAction?.Invoke(entry);
                        },
                    }
                },
            };
        }

        private static class BranchListItemStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVE_BRANCH_BADGE = new EzLocalizationManager.EzLocalisableString("当前启用", "Active");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDDEN_BRANCH_BADGE = new EzLocalizationManager.EzLocalisableString("已应用隐藏", "Hide Applied");
            internal static readonly EzLocalizationManager.EzLocalisableString COLLECTION_PENDING_BADGE = new EzLocalizationManager.EzLocalisableString("未生成", "Not Generated");

            internal static readonly EzLocalizationManager.EzLocalisableString SOURCE_COLLECTION_PREFIX = new EzLocalizationManager.EzLocalisableString("来源收藏夹：", "Source collection: ");
            internal static readonly EzLocalizationManager.EzLocalisableString UNKNOWN_COLLECTION = new EzLocalizationManager.EzLocalisableString("未知收藏夹", "Unknown collection");

            internal static readonly EzLocalizationManager.EzLocalisableString GENERATE_HINT = new EzLocalizationManager.EzLocalisableString("点击下方“生成分支曲库”使用当前 mods 创建。",
                "Use the generate button below to create a branch library with the current mods.");

            internal static readonly EzLocalizationManager.EzLocalisableString BEATMAPS_UNIT = new EzLocalizationManager.EzLocalisableString("张谱面", "beatmaps");

            internal static readonly EzLocalizationManager.EzLocalisableString APPLY_BRANCH_HIDE_TOOLTIP =
                new EzLocalizationManager.EzLocalisableString("按收藏夹应用隐藏", "Apply hide using the source collection");

            internal static readonly EzLocalizationManager.EzLocalisableString RESTORE_BRANCH_HIDE_TOOLTIP =
                new EzLocalizationManager.EzLocalisableString("按收藏夹取消隐藏", "Remove hide using the source collection");

            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATE_BRANCH_TOOLTIP = new EzLocalizationManager.EzLocalisableString("启用分支曲库", "Activate branch library");
            internal static readonly EzLocalizationManager.EzLocalisableString DEACTIVATE_BRANCH_TOOLTIP = new EzLocalizationManager.EzLocalisableString("停用分支曲库", "Deactivate branch library");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_BRANCH_TOOLTIP = new EzLocalizationManager.EzLocalisableString("删除分支曲库", "Delete branch library");

            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_COLLECTION_LOCAL_BEATMAPS_TOOLTIP = new EzLocalizationManager.EzLocalisableString("删除收藏夹命中的本地谱面（不修改收藏夹）",
                "Delete local beatmaps matched by this collection (collection list remains unchanged)");
        }
    }

    public partial class RowActionButton : OsuClickableContainer
    {
        private readonly IconUsage icon;
        private readonly ButtonStyle style;
        private Color4 darkenedColour;
        private Color4 normalColour;
        private Drawable background = null!;

        public Func<Vector2, bool> IsTextBoxHovered = null!;

        public RowActionButton(IconUsage icon, LocalisableString tooltipText, ButtonStyle style)
        {
            this.icon = icon;
            this.style = style;
            TooltipText = tooltipText;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            normalColour = style switch
            {
                ButtonStyle.Danger => colours.Red,
                ButtonStyle.Warning => colours.Yellow,
                ButtonStyle.Disabled => colours.GreySeaFoamLight,
                _ => colours.BlueLight,
            };

            darkenedColour = normalColour.Darken(0.9f);

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 10,
                Children = new[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = darkenedColour,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(10),
                        Icon = icon,
                    }
                }
            };
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => base.ReceivePositionalInputAt(screenSpacePos) && !IsTextBoxHovered(screenSpacePos);

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(normalColour, 100, Easing.Out);
            return false;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(darkenedColour, 100, Easing.Out);
        }

        protected override bool OnClick(ClickEvent e)
        {
            background.FlashColour(Color4.White, 150);
            return base.OnClick(e);
        }
    }

    public partial class NameDisplayBox : CompositeDrawable
    {
        private readonly string text;

        public NameDisplayBox(string text)
        {
            this.text = text;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.GreySeaFoamDarker.Darken(0.3f),
                },
                new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    X = 14,
                    Padding = new MarginPadding { Right = 14 },
                    Text = text,
                    Font = OsuFont.Default.With(size: 18, weight: FontWeight.Medium),
                }
            };
        }
    }

    public partial class EmptyStateText : OsuSpriteText
    {
        public EmptyStateText()
        {
            Margin = new MarginPadding(20);
            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Medium);
            Colour = Color4.White.Opacity(0.7f);
        }
    }

    public partial class SectionHeaderText : OsuSpriteText
    {
        public SectionHeaderText()
        {
            Margin = new MarginPadding { Top = 4, Bottom = 8, Left = 4 };
            Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold);
            Colour = Color4.White.Opacity(0.75f);
        }
    }
}
