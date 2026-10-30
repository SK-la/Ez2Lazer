// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.SkinEditor;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// HUD 栏内容：跟随选中组件显示官方同类设置控件。
    /// </summary>
    public partial class EzSkinEditorHudWorkspaceSection : FillFlowContainer
    {
        [Resolved(canBeNull: true)]
        private EzSkinEditorHudLayer? hudLayer { get; set; }

        public EzSkinEditorHudWorkspaceSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 12);
            Padding = new MarginPadding(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (hudLayer == null)
            {
                showMessage(EzEditorStrings.SIDEBAR_HUD_PLACEHOLDER);
                return;
            }

            hudLayer.LayerVisible.BindValueChanged(_ => rebuild(), true);
            hudLayer.SelectedComponents.BindCollectionChanged((_, _) => Scheduler.AddOnce(rebuild), true);
        }

        private void rebuild()
        {
            Clear();

            if (hudLayer == null)
            {
                showMessage(EzEditorStrings.SIDEBAR_HUD_PLACEHOLDER);
                return;
            }

            if (!hudLayer.LayerVisible.Value)
            {
                showMessage(EzEditorStrings.SIDEBAR_HUD_LAYER_OFF);
                return;
            }

            var selected = hudLayer.SelectedComponents.OfType<Drawable>().ToArray();

            if (selected.Length == 0)
            {
                showMessage(EzEditorStrings.SIDEBAR_HUD_NO_SELECTION);
                return;
            }

            foreach (var component in selected)
                Add(new SkinSettingsToolbox(component));
        }

        private void showMessage(LocalisableString text)
        {
            Child = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = text,
                Font = Graphics.OsuFont.Default.With(size: 14),
            };
        }
    }
}
