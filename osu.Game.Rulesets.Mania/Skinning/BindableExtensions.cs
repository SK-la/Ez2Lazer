using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class BindableExtensions
    {
        public static Bindable<float> ConvertToFloatBindable(this IBindable<double> doubleBindable)
        {
            var floatBindable = new Bindable<float>();
            // 将 double 绑定到 float 绑定上，初始绑定时转换一次，并且之后值更新时同步
            doubleBindable.BindValueChanged(e => floatBindable.Value = (float)e.NewValue, true);
            return floatBindable;
        }
    }
}
