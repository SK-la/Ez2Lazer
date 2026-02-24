using osu.Framework.Bindables;

namespace osu.Game.LAsEzExtensions.HUD
{
    /// <summary>
    /// 战未来预览组件接口，提供预览用的纹理信息
    /// </summary>
    public interface IPreviewable
    {
        Bindable<string> TextureNameBindable { get; }
        string TextureBasePath { get; }
    }
}
