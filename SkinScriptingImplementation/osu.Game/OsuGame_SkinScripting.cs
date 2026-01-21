using osu.Framework.Allocation;
using osu.Game.Skinning.Scripting;

namespace osu.Game
{
    public partial class OsuGame
    {
        private SkinScriptingOverlayRegistration scriptingRegistration;

        [BackgroundDependencyLoader]
        private void loadSkinScripting()
        {
            // 添加皮肤脚本设置注册组件
            Add(scriptingRegistration = new SkinScriptingOverlayRegistration());
        }
    }
}
