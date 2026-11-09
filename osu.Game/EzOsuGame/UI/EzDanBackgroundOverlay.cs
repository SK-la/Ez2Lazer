// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// Decorative chart-dan badges on song-select background (「段位痴」).
    /// </summary>
    public partial class EzDanBackgroundOverlay : CompositeDrawable
    {
        // private const float badge_display_height = 420f;
        // private const float watermark_alpha = 0.22f;

        public BindableBool BackgroundRevealed { get; } = new BindableBool();

        private Sprite badge = null!;

        private Bindable<bool> enabled = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private EzSkillProvider? skillProvider { get; set; }

        [Resolved]
        private EzResourceStore resources { get; set; } = null!;

        public EzDanBackgroundOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            InternalChildren = new Drawable[]
            {
                badge = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    // FillMode = FillMode.Fit,
                    Alpha = 0,
                    Scale = new Vector2(0.7f),
                }
            };

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.SkillDanFanatic);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled.BindValueChanged(_ => refresh(), true);
            BackgroundRevealed.BindValueChanged(_ => refresh(), true);
            beatmap.BindValueChanged(_ => refresh(), true);
            mods.BindValueChanged(_ => refresh(), true);
        }

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     bool editorActive = EzSkinEditorActiveState.Active;
        //
        //     if (editorActive != lastSkinEditorActive)
        //     {
        //         lastSkinEditorActive = editorActive;
        //         refresh();
        //     }
        // }

        // private bool lastSkinEditorActive;
        // private Vector2 lastDrawSize;

        // protected override void UpdateAfterChildren()
        // {
        //     base.UpdateAfterChildren();
        //
        //     if (DrawSize != lastDrawSize)
        //     {
        //         lastDrawSize = DrawSize;
        //         refresh();
        //     }
        // }

        private void refresh()
        {
            if (!enabled.Value || BackgroundRevealed.Value)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1;

            var info = beatmap.Value.BeatmapInfo;

            if (info == null || skillProvider == null)
            {
                setBadge(badge, null);
                return;
            }

            if (isDanBeatmap(info))
            {
                setBadge(badge, null);
                return;
            }

            skillProvider.TryGetChartDanForUi(info, out var chartDan, allowMemoryCompute: true);

            int keyCount = 0;

            if (chartDan is { KeyCount: > 0 })
                keyCount = chartDan.KeyCount;

            if (keyCount <= 0 && info.Difficulty.CircleSize > 0)
                keyCount = (int)Math.Round(info.Difficulty.CircleSize);

            EzDanSide side = chartDan?.HasSide(EzDanSide.Ln) == true ? EzDanSide.Ln : EzDanSide.Rc;
            setBadge(badge, tryLoadBadge(keyCount, side, chartDan?.LabelFor(side)));
        }

        private static bool isDanBeatmap(BeatmapInfo info)
        {
            return info.Metadata != null
                   && info.Metadata.Title.Contains("Dan ", StringComparison.OrdinalIgnoreCase)
                   && (string.Equals(info.Metadata.Artist, "Various Artists", StringComparison.OrdinalIgnoreCase));
        }

        private Texture? tryLoadBadge(int keyCount, EzDanSide side, string? label)
        {
            if (keyCount <= 0 || string.IsNullOrEmpty(label))
                return null;

            string bare = EzDanLadders.BareLabel(label);
            string? relative = EzDanLadders.TryGetTexturePath(keyCount, side, bare);
            if (relative == null)
                return null;

            return resources.Get(relative, EzTextureUsage.Badge, originalSize: true);
        }

        private void setBadge(Sprite sprite, Texture? texture)
        {
            if (texture == null)
            {
                sprite.Texture = null;
                sprite.Alpha = 0;
                return;
            }

            sprite.Texture = texture;
            // sprite.Height = badge_display_height;
            // sprite.Width = texture.Width / (float)texture.Height * badge_display_height;
            // sprite.Position = Vector2.Zero;
            sprite.Alpha = 1f;
        }
    }
}
