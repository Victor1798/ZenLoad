using System.IO;
using Forms = System.Windows.Forms;

namespace ZenLoad.Services;

public sealed class WindowsFolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialPath)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde se guardarán los archivos",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.SelectedPath = initialPath;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
