using osu.Framework.Bindables;

namespace osu.Game.Skinning.Components
{
    public interface IPreviewable
    {
        Bindable<string> TextureNameBindable { get; }
        string TextureBasePath { get; }
    }
}
