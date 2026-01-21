using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Skinning.Scripting.Overlays;

namespace osu.Game.Skinning.Scripting
{
    /// <summary>
    /// 负责注册皮肤脚本设置到设置界面。
    /// </summary>
    public class SkinScriptingOverlayRegistration : Component
    {
        [Resolved]
        private SettingsOverlay settingsOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 皮肤脚本设置部分已经在SkinSection中集成，无需额外操作
        }
    }
}
