// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class ManiaEzSkinEditorNoteRulesetProfile : IEzSkinEditorNoteRulesetProfile
    {
        private static readonly EzSkinEditorNotePart[] supported_parts =
        {
            EzSkinEditorNotePart.Note,
            EzSkinEditorNotePart.HoldHead,
            EzSkinEditorNotePart.HoldBody,
            EzSkinEditorNotePart.HoldTail,
        };

        private static readonly EzSkinEditorNoteVariant[] legacy_variants =
        {
            new EzSkinEditorNoteVariant("1", "1"),
            new EzSkinEditorNoteVariant("2", "2"),
            new EzSkinEditorNoteVariant("S", "S"),
        };

        private static readonly EzSkinEditorNoteVariant[] ez_variants =
        {
            new EzSkinEditorNoteVariant(nameof(EzColumnType.A), "A"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.B), "B"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.S), "S"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.E), "E"),
            new EzSkinEditorNoteVariant(nameof(EzColumnType.P), "P"),
        };

        private readonly EzSkinLNEditorProvider previewProvider = new EzSkinLNEditorProvider();

        public int RulesetOnlineId => new ManiaRuleset().RulesetInfo.OnlineID;

        public RulesetInfo RulesetInfo => new ManiaRuleset().RulesetInfo;

        public IReadOnlyList<EzSkinEditorNotePart> SupportedParts => supported_parts;

        public IReadOnlyList<EzSkinEditorNoteVariant> GetVariants(ISkin skin, EzSkinEditorNotePart part) =>
            isEzSkin(skin) ? ez_variants : legacy_variants;

        public string GetDefaultVariantId(ISkin skin, EzSkinEditorNotePart part) =>
            isEzSkin(skin) ? nameof(EzColumnType.A) : "1";

        public Drawable CreateNotePreview(ISkin skin, EzSkinEditorNotePreviewRequest request) =>
            previewProvider.CreateNotePreview(skin, request);

        public Drawable CreateRulesetSettingsContent() => new ManiaNoteRulesetSettingsSection();

        private static bool isEzSkin(ISkin skin) =>
            skin is EzStyleProSkin or Ez2Skin or SbISkin;

        private partial class ManiaNoteRulesetSettingsSection : FillFlowContainer
        {
            [Resolved]
            private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

            public ManiaNoteRulesetSettingsSection()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new osuTK.Vector2(8);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var config = (ManiaRulesetConfigManager)rulesetConfigCache.GetConfigFor(new ManiaRuleset())!;

                Add(new SettingsCheckbox
                {
                    LabelText = RulesetSettingsStrings.TimingBasedColouring,
                    Current = config.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring),
                });
            }
        }
    }
}
