// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
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

        protected override HitWindows CreateHitWindows() => new ManiaHitWindows();

        #region LegacyBeatmapEncoder

        float IHasXPosition.X
        {
            get => Column;
            set => Column = (int)value;
        }

        #endregion
    }
}
