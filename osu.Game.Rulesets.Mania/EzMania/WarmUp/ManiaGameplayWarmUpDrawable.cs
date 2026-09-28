// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Editor;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.WarmUp
{
    /// <summary>
    /// 规则集侧的通用预热 drawable：用当前皮肤 transformer 产出 gameplay 组件并离屏加载。
    /// </summary>
    /// <remarks>
    /// 与皮肤无关——EzPro / Ez2 / SbI / Legacy / Argon / scripted 全走各自的 transformer，
    /// 因此不再需要任何写死的 <c>note/{noteSet}</c> / <c>Stage/{stage}</c> 路径枚举。
    /// 组件返回空（该皮肤不提供）时自然跳过。
    ///
    /// 依赖由一个最小宿主（复用编辑器预览用的 <see cref="EzSkinLNEditorProvider.PreviewDependencyContainer"/>）提供
    /// <c>Column</c> / <c>StageDefinition</c> / <c>IScrollingInfo</c> / <c>IGameplayClock</c>，
    /// 让 <c>[Resolved] Column</c> 等依赖在 Player 树外也能解析。
    /// </remarks>
    public partial class ManiaGameplayWarmUpDrawable : Container
    {
        // 预热用 LN 长度。只影响中部布局与 tick 数量，不参与纹理探测，取一个铺得开的值即可。
        private const double warm_up_hold_duration = 500;

        public ManiaGameplayWarmUpDrawable(ManiaRuleset ruleset, ISkin skin, ManiaBeatmap beatmap)
        {
            // 铺满宿主：皮肤组件的纹理创建大多挂在 DrawSize/布局上，尺寸为 0 会让它们直接跳过。
            RelativeSizeAxes = Axes.Both;

            int keyCount = Math.Max(1, beatmap.GetStageForColumnIndex(0).Columns);

            var host = new EzSkinLNEditorProvider.PreviewDependencyContainer(keyCount, 0, ManiaAction.Key1);

            ISkin? transformer = ruleset.CreateSkinTransformer(skin, beatmap);

            if (transformer != null)
            {
                foreach (ManiaSkinComponents component in Enum.GetValues<ManiaSkinComponents>())
                {
                    if (isOwnedByHitObject(component))
                        continue;

                    Drawable? drawable = transformer.GetDrawableComponent(new ManiaSkinComponentLookup(component));

                    if (drawable != null)
                        host.Add(drawable);
                }

                host.Add(createNoteDrawable());
                host.Add(createHoldNoteDrawable());

                // 组件必须拿到「当前皮肤的 transformer」而不是全局皮肤：否则它们会去查错皮肤的资源，
                // 预热等于白做（编辑器预览同样是这么包的，见 EzSkinLNEditorProvider）。
                Child = new SkinProvidingContainer(transformer)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = host,
                };

                return;
            }

            Child = host;
        }

        // note 系列的组件不是独立存在的：它们在 BDL 里注入 owner 的 DrawableHitObject 并按 owner 初始化，
        // 例如 EzHoldNoteMiddle 强转 DrawableHoldNote、Legacy 的 body 直接要求 DrawableHitObject 非空。
        // 预热宿主里没有 DHO，直接把这些组件挂在依赖容器下会注入 null（允许注入 null 的组件）或解析失败（严格 DI 的组件）。
        // 所以这几项一律交给真正的 DHO 自己产出——与进局、编辑器预览同一条路径。
        private static bool isOwnedByHitObject(ManiaSkinComponents component) => component switch
        {
            ManiaSkinComponents.Note => true,
            ManiaSkinComponents.HoldNoteHead => true,
            ManiaSkinComponents.HoldNoteTail => true,
            ManiaSkinComponents.HoldNoteBody => true,
            _ => false,
        };

        private static DrawableHitObject createNoteDrawable()
        {
            var hitObject = new Note { StartTime = 0, Column = 0 };
            hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return new DrawableNote(hitObject)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 1,
            };
        }

        // HoldNote 的 DHO 在应用时会按 HitObject.NestedHitObjects 建出 head / tail / body，
        // 这三个组件的纹理创建因此一并被覆盖，不需要再各自单独预热。
        private static DrawableHitObject createHoldNoteDrawable()
        {
            var hitObject = new HoldNote { StartTime = 0, Duration = warm_up_hold_duration, Column = 0 };
            hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return new DrawableHoldNote(hitObject)
            {
                RelativeSizeAxes = Axes.Both,
            };
        }
    }
}
