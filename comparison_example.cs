namespace Example
{
    // 主文件
    public partial class FilterControl
    {
        private string privateField = "只有嵌套类能访问";
        private void privateMethod() { }
    }

    // 情况1：正确的嵌套类写法
    public partial class FilterControl
    {
        public partial class DifficultyRangeSlider
        {
            public void AccessParent()
            {
                // ✅ 可以访问外层类的私有成员
                var field = privateField;  // 编译成功
                privateMethod();           // 编译成功
            }
        }
    }

    // 情况2：错误的独立类写法
    public partial class DifficultyRangeSlider  // 这是独立的类，不是嵌套类
    {
        public void AccessParent()
        {
            // ❌ 无法访问 FilterControl 的私有成员
            // var field = privateField;  // 编译错误！
            // privateMethod();           // 编译错误！
        }
    }
}
