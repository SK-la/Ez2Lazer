// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 左侧二级场景垂直导航栏。列出当前一级场景组内的二级场景，与场景栏共用
    /// <see cref="CurrentScene"/>；选中项与场景值双向同步。
    /// </summary>
    public partial class EzSkinEditorSceneRail : EzVerticalRail
    {
        /// <summary>
        /// Shared with the scene bar. Setting it re-groups this rail; the rail's buttons write back here.
        /// </summary>
        public Bindable<EzEditorSceneType> CurrentScene { get; } = new Bindable<EzEditorSceneType>();

        private IReadOnlyList<IEzSkinEditorSceneStrategy> scenes = new List<IEzSkinEditorSceneStrategy>();

        // Runtime visibility per scene (screen-driven), separate from each strategy's static availability.
        private readonly Dictionary<EzEditorSceneType, bool> runtimeVisible = new Dictionary<EzEditorSceneType, bool>();

        private EzEditorSceneGroup? group;

        // Guards the SelectedIndex <-> CurrentScene two-way sync against feedback loops.
        private bool syncing;

        public EzSkinEditorSceneRail()
        {
            SelectionChanged += onSelectionChanged;
            CurrentScene.BindValueChanged(onCurrentSceneChanged, true);
        }

        /// <summary>
        /// Shows or hides a scene without rebuilding the rail or resetting the current scene.
        /// </summary>
        public void SetSceneVisible(EzEditorSceneType scene, bool visible)
        {
            runtimeVisible[scene] = visible;

            if (group == null || EzSkinEditorSceneRegistry.GroupOf(scene) != group)
                return;

            int index = indexOf(scene);

            if (index < 0)
                return;

            SetItemVisible(index, isVisible(scenes[index]));
        }

        private void onCurrentSceneChanged(ValueChangedEvent<EzEditorSceneType> change)
        {
            if (syncing)
                return;

            syncing = true;

            try
            {
                var newGroup = EzSkinEditorSceneRegistry.GroupOf(change.NewValue);

                if (newGroup != group)
                    rebuildForGroup(newGroup);

                selectScene(change.NewValue);
            }
            finally
            {
                syncing = false;
            }
        }

        private void onSelectionChanged(int index)
        {
            if (syncing || index < 0 || index >= scenes.Count)
                return;

            syncing = true;

            try
            {
                CurrentScene.Value = scenes[index].SceneType;
            }
            finally
            {
                syncing = false;
            }
        }

        private void rebuildForGroup(EzEditorSceneGroup newGroup)
        {
            group = newGroup;
            scenes = EzSkinEditorSceneRegistry.ForGroup(newGroup).ToList();

            SetItems(scenes
                     .Select(s => ((LocalisableString)s.TabTitle, isVisible(s)))
                     .ToList());
        }

        private void selectScene(EzEditorSceneType scene)
        {
            int index = indexOf(scene);

            if (index >= 0)
                SelectedIndex.Value = index;
        }

        private int indexOf(EzEditorSceneType scene)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].SceneType == scene)
                    return i;
            }

            return -1;
        }

        private bool isVisible(IEzSkinEditorSceneStrategy strategy) =>
            strategy.IsAvailable && (!runtimeVisible.TryGetValue(strategy.SceneType, out bool visible) || visible);
    }
}
