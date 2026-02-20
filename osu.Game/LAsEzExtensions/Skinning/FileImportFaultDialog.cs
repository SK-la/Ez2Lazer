using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.LAsEzExtensions.Skinning
{
    public partial class FileImportFaultDialog : PopupDialog
    {
        public FileImportFaultDialog(string errorMessage)
        {
            Icon = FontAwesome.Regular.TimesCircle;
            HeaderText = "Import failed";
            BodyText = errorMessage;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton(),
            };
        }
    }
}
