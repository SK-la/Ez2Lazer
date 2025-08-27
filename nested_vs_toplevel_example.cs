// 示例：嵌套类 vs 顶级类的区别

namespace Example
{
    // ========== 情况1：嵌套类设计（osu! 当前使用） ==========
    public partial class FilterControl : OverlayContainer
    {
        private readonly Dictionary<string, object> controlState = new();
        private bool isInitialized = false;

        // 嵌套类 - 逻辑上是 FilterControl 的一部分
        public partial class KeyModeFilterTabControl : CompositeDrawable
        {
            public void AccessParentState()
            {
                // ✅ 可以访问外层类的私有成员
                // 注意：需要通过外层类实例访问非静态成员
            }

            public void UpdateParentControl(FilterControl parent)
            {
                // ✅ 可以访问私有成员
                parent.controlState["keyMode"] = "updated";
                if (parent.isInitialized) { /* ... */ }
            }
        }

        // 其他相关的嵌套类
        public partial class DifficultyRangeSlider : ShearedRangeSlider { }
        public partial class SongSelectSearchTextBox : ShearedFilterTextBox { }
    }

    // ========== 情况2：顶级类设计（替代方案） ==========
    public class FilterControl : OverlayContainer
    {
        internal readonly Dictionary<string, object> controlState = new(); // 必须改为 internal
        internal bool isInitialized = false; // 必须改为 internal
    }

    // 独立的顶级类
    public class KeyModeFilterTabControl : CompositeDrawable
    {
        public void UpdateParentControl(FilterControl parent)
        {
            // ✅ 只能访问 internal/public 成员
            parent.controlState["keyMode"] = "updated";
            if (parent.isInitialized) { /* ... */ }

            // ❌ 无法访问 private 成员
            // parent.somePrivateField = value; // 编译错误
        }
    }

    public class DifficultyRangeSlider : ShearedRangeSlider { }
    public class SongSelectSearchTextBox : ShearedFilterTextBox { }
}
