// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects
{
    public abstract class ManiaHitObject : HitObject, IHasColumn, IHasXPosition
    {
        private HitObjectProperty<int> column;

        public Bindable<int> ColumnBindable => column.Bindable;

        public virtual int Column
        {
            get => column.Value;
            set => column.Value = value;
        }

        public override HitObject Clone()
        {
            var clone = (ManiaHitObject)base.Clone();

            // HitObjectProperty 是值类型但惰性持有 Bindable<int>，MemberwiseClone 会连 bindable 一起共享：
            // 不重置的话改副本的 Column 会直接改到原对象（drawable 也 BindTo 在同一个 bindable 上）。
            clone.column = new HitObjectProperty<int>(Column);

            return clone;
        }

        // 转换期不读全局 HitMode：真实窗口一律由 ManiaWindowBaker 在绑定期按当局 hitmode 烘焙
        // （live 走 BindForLive、仿真走 BindForSimulation）。这里显式用 Lazer 起步，省掉每个音符一次
        // 全局配置查询与窗口计算——那批结果在绑定时会被整体覆盖，纯属浪费。
        protected override HitWindows CreateHitWindows() => new ManiaHitWindows(EzEnumHitMode.Lazer);

        #region LegacyBeatmapEncoder

        float IHasXPosition.X
        {
            get => Column;
            set => Column = (int)value;
        }

        #endregion
    }
}
