using osu.Framework.Bindables;

namespace osu.Game.LAsEzExtensions.Screens
{
    public interface IPreviewable
    {
        Bindable<string> TextureNameBindable { get; }
        string TextureBasePath { get; }
    }
}
