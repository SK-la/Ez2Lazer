// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层。目标来自插拔进来的当前屏（及 Ez 图层），不再自建空 ScoreProcessor。
    /// </summary>
    public partial class EzSkinEditorHudLayer : CompositeDrawable
    {
        /// <summary>
        /// 蓝图是否接受输入。由场景是否为 HUD 场景与 HUD layer 开关共同决定；与蓝图是否显示无关。
        /// </summary>
        public readonly BindableBool InteractionEnabled = new BindableBool();

        /// <summary>
        /// HUD layer 开关本身：控制蓝图显示隐藏。
        /// </summary>
        public readonly BindableBool LayerVisible = new BindableBool(true);

        public IBindableList<ISerialisableDrawable> SelectedComponents => selectedComponents;

        private readonly BindableList<ISerialisableDrawable> selectedComponents = new BindableList<ISerialisableDrawable>();

        public override bool PropagatePositionalInputSubTree => InteractionEnabled.Value;

        public override bool PropagateNonPositionalInputSubTree => InteractionEnabled.Value;

        private Container blueprintHost = null!;
        private SkinManager skins = null!;
        private OsuScreen? liveScreen;
        private EzLayoutLayer? layoutLayer;
        private RulesetInfo? ruleset;

        public EzSkinEditorHudLayer()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            skins = skinManager;
            InternalChild = blueprintHost = new Container { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            LayerVisible.BindValueChanged(_ => refreshBlueprints(), true);
        }

        public void AttachLiveTargets(OsuScreen? screen, EzLayoutLayer? ezLayout)
        {
            if (!ReferenceEquals(layoutLayer, ezLayout))
            {
                if (layoutLayer != null)
                    layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;

                layoutLayer = ezLayout;

                if (layoutLayer != null)
                    layoutLayer.LayoutTargetsChanged += onLayoutTargetsChanged;
            }

            liveScreen = screen;
            refreshBlueprints();
        }

        public void DetachLiveTargets() => AttachLiveTargets(null, null);

        private void onLayoutTargetsChanged()
        {
            if (IsLoaded)
                Schedule(refreshBlueprints);
        }

        public void SetRuleset(RulesetInfo? newRuleset)
        {
            if (ruleset?.Equals(newRuleset) == true)
                return;

            ruleset = newRuleset;
            refreshBlueprints();
        }

        public void SetVisible(bool visible)
        {
            LayerVisible.Value = visible;
            this.FadeTo(visible ? 1 : 0, 200, Easing.OutQuint);
        }

        private void refreshBlueprints()
        {
            if (!IsLoaded)
                return;

            blueprintHost.Clear();
            selectedComponents.Clear();

            // 显示由 HUD layer 开关决定，交互由场景 + 开关共同决定；两者互不代管。
            if (!LayerVisible.Value)
                return;

            foreach (var target in EnumerateTargets())
                blueprintHost.Add(new EzSkinEditorHudBlueprintContainer(target, selectedComponents, PersistLayoutToCurrentSkin));
        }

        public IEnumerable<SkinnableContainer> EnumerateTargets()
        {
            if (liveScreen is Player player && player.IsLoaded)
            {
                foreach (var c in player.ChildrenOfType<SkinnableContainer>())
                    yield return c;
            }

            if (layoutLayer != null)
            {
                foreach (var c in layoutLayer.LayoutContainers.Where(c => c.Alpha > 0))
                    yield return c;
            }
        }

        public void PersistLayoutToCurrentSkin()
        {
            var skin = skins.CurrentSkin.Value;

            foreach (var target in EnumerateTargets())
            {
                if (target is EzLayoutContainer ez)
                {
                    layoutLayer?.Store.Save(ez);
                    continue;
                }

                if (target.ComponentsLoaded)
                    skin.UpdateDrawableTarget(target);
            }

            skins.Save(skin);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (layoutLayer != null)
                layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
        }
    }
}
