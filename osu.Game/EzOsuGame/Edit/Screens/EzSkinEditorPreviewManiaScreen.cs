// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Screens
{
    /// <summary>
    /// 选歌组「谱面预览」的插屏预览。只做 mania 全谱预览，渲染器自行按屏内可用空间缩放。
    /// 数据由协调器解析并注入；不依赖编辑器的 PreviewState，切换预览谱面时用
    /// <see cref="SetSource"/> 原地重建，不重插屏幕。
    /// </summary>
    public partial class EzSkinEditorPreviewManiaScreen : OsuScreen
    {
        public override bool AllowExternalScreenChange => true;

        public override bool HideOverlaysOnEnter => false;

        public override bool? AllowGlobalTrackControl => false;

        private readonly EzPreviewManiaSettings settings;

        private IWorkingBeatmap? workingBeatmap;
        private RulesetInfo? rulesetInfo;

        private Container viewport = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        public EzSkinEditorPreviewManiaScreen(IWorkingBeatmap? workingBeatmap, RulesetInfo? rulesetInfo, EzPreviewManiaSettings settings)
        {
            this.workingBeatmap = workingBeatmap;
            this.rulesetInfo = rulesetInfo;
            this.settings = settings;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        protected override void LogoArriving(OsuLogo? logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);
            logo?.FadeOut();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(viewport = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rebuild();

            // 设置即改即存且立即回灌预览：任一 bindable 变化触发一次重绘。
            settings.Style.BindValueChanged(_ => applySettings());
            settings.ThicknessScale.BindValueChanged(_ => applySettings());
            settings.LaneBackgroundColour.BindValueChanged(_ => applySettings());

            settings.NoteColour.BindValueChanged(_ => applySettings());
            settings.HoldHeadColour.BindValueChanged(_ => applySettings());
            settings.HoldBodyColour.BindValueChanged(_ => applySettings());
            // settings.HoldTailColour.BindValueChanged(_ => applySettings());
        }

        /// <summary>
        /// 换掉渲染的谱面（例如用户在编辑器里切换了预览谱面），原地重建内容。
        /// </summary>
        public void SetSource(IWorkingBeatmap? beatmap, RulesetInfo? ruleset)
        {
            bool unchanged = workingBeatmap?.BeatmapInfo.Hash == beatmap?.BeatmapInfo.Hash
                             && rulesetInfo?.ShortName == ruleset?.ShortName;

            if (unchanged)
                return;

            workingBeatmap = beatmap;
            rulesetInfo = ruleset;

            if (IsLoaded)
                rebuild();
        }

        private void rebuild()
        {
            var renderer = new StaticFullMapPreviewRenderer();

            viewport.Child = renderer;

            RulesetInfo? ruleset = resolveRulesetForRender(rulesetInfo, workingBeatmap);

            if (workingBeatmap == null || ruleset == null || !EzBeatmapPreviewModes.IsManiaRuleset(ruleset))
            {
                viewport.Child = createPlaceholder(ManiaBeatmapPreviewStrings.BEATMAP_NOT_MANIA);
                return;
            }

            IBeatmap playableBeatmap;

            try
            {
                playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset);
            }
            catch
            {
                viewport.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_LOAD_FAILED);
                return;
            }

            ManiaPreviewData data = ManiaPreviewGeometryBuilder.Build(playableBeatmap);

            if (data.Notes.Count == 0)
            {
                viewport.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
                return;
            }

            renderer.SetSettings(settings);
            renderer.SetData(data);
        }

        private void applySettings()
        {
            if (viewport.Child is StaticFullMapPreviewRenderer renderer)
                renderer.SetSettings(settings);
        }

        private RulesetInfo? resolveRulesetForRender(RulesetInfo? ruleset, IWorkingBeatmap? beatmap)
        {
            if (beatmap?.BeatmapInfo.Ruleset is not RulesetInfo beatmapRuleset)
                return ruleset;

            return EzSkinEditorBeatmapPicker.ResolveRulesetForPreview(rulesets, beatmapRuleset, ruleset) ?? ruleset;
        }

        private static OsuSpriteText createPlaceholder(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };
    }
}
