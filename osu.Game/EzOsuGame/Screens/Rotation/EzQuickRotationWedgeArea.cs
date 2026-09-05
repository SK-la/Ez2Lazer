// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    /// <summary>
    /// Song-select style wedge stack (title + details) for the quick rotation pick screen.
    /// </summary>
    public partial class EzQuickRotationWedgeArea : CompositeDrawable
    {
        private readonly Bindable<WorkingBeatmap> working = new Bindable<WorkingBeatmap>();
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private readonly Bindable<IReadOnlyList<Mod>> mods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());
        private readonly Bindable<SongSelect.BeatmapSetLookupResult?> onlineLookupResult = new Bindable<SongSelect.BeatmapSetLookupResult?>(SongSelect.BeatmapSetLookupResult.Completed(null));

        private BeatmapTitleWedge titleWedge = null!;
        private BeatmapDetailsArea detailsArea = null!;
        private FillFlowContainer wedgeFlow = null!;

        public EzQuickRotationWedgeArea(WorkingBeatmap? initialWorking = null, RulesetInfo? initialRuleset = null, IReadOnlyList<Mod>? initialMods = null)
        {
            RelativeSizeAxes = Axes.Y;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Masking = true;

            if (initialWorking != null)
                working.Value = initialWorking;

            if (initialRuleset != null)
                ruleset.Value = initialRuleset;

            if (initialMods != null)
                mods.Value = initialMods;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmapManager)
        {
            var session = EzQuickRotationCoordinator.Session;

            working.Value ??= beatmapManager.GetWorkingBeatmap(null);
            ruleset.Value ??= session.Ruleset;
            mods.Value ??= session.BaseMods;

            Padding = new MarginPadding
            {
                Top = 40,
                Right = 20,
            };

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Shear = OsuGame.SHEAR,
                Padding = new MarginPadding
                {
                    Top = -SongSelect.CORNER_RADIUS_HIDE_OFFSET,
                    Left = -SongSelect.CORNER_RADIUS_HIDE_OFFSET,
                },
                Child = wedgeFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Padding = new MarginPadding { Right = 40 },
                    Children = new Drawable[]
                    {
                        new ShearAligningWrapper(titleWedge = new BeatmapTitleWedge()),
                        new ShearAligningWrapper(detailsArea = new BeatmapDetailsArea
                        {
                            RelativeSizeAxes = Axes.X,
                        }),
                    },
                },
            };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs((IBindable<WorkingBeatmap>)working);
            dependencies.CacheAs((IBindable<RulesetInfo>)ruleset);
            dependencies.CacheAs((IBindable<IReadOnlyList<Mod>>)mods);
            dependencies.CacheAs((IBindable<SongSelect.BeatmapSetLookupResult?>)onlineLookupResult);
            dependencies.CacheAs<ISongSelect>(new EzQuickRotationSongSelectStub());
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateDetailsHeight();
        }

        protected override void Update()
        {
            base.Update();
            updateWedgeWidth();
            updateDetailsHeight();
        }

        /// <summary>
        /// Match <see cref="SongSelect"/> left grid column: 50% relative width capped at 700 (+ widescreen bonus).
        /// </summary>
        private void updateWedgeWidth()
        {
            if (Parent == null)
                return;

            float widescreenBonusWidth = Math.Max(0, Parent.DrawWidth / Parent.DrawHeight - 2f);
            float maxWedgeWidth = 700 + widescreenBonusWidth * 100;
            Width = Math.Min(Parent.DrawWidth * 0.5f, maxWedgeWidth);
        }

        private void updateDetailsHeight()
        {
            float remaining = Math.Max(0, DrawHeight - titleWedge.DrawHeight - wedgeFlow.Spacing.Y);
            if (Math.Abs(detailsArea.Height - remaining) > 0.5f)
                detailsArea.Height = remaining;
        }

        public void UpdateSelection(WorkingBeatmap workingBeatmap,
                                    RulesetInfo rulesetInfo,
                                    IReadOnlyList<Mod> modList,
                                    SongSelect.BeatmapSetLookupResult? lookupResult = null)
        {
            working.Value = workingBeatmap;
            ruleset.Value = rulesetInfo;
            mods.Value = modList;
            onlineLookupResult.Value = lookupResult ?? SongSelect.BeatmapSetLookupResult.Completed(
                EzQuickRotationApiBeatmapFactory.TryCreateBeatmapSet(workingBeatmap.BeatmapInfo));

            if (!titleWedge.IsPresent)
                titleWedge.Show();

            if (!detailsArea.IsPresent)
                detailsArea.Show();
        }
    }
}
