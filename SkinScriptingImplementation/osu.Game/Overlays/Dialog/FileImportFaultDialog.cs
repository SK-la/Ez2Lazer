using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Dialog
{
    /// <summary>
    /// 文件导入失败时显示的对话框。
    /// </summary>
    public partial class FileImportFaultDialog : PopupDialog
    {
        /// <summary>
        /// 初始化 <see cref="FileImportFaultDialog"/> 类的新实例。
        /// </summary>
        /// <param name="errorMessage">错误信息。</param>
        public FileImportFaultDialog(string errorMessage)
        {
            Icon = FontAwesome.Regular.TimesCircle;
            HeaderText = "导入失败";
            BodyText = errorMessage;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "确定",
                }
            };
        }
    }
}
