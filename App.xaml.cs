using ZenLoad.Models;
using ZenLoad.Services;
using ZenLoad.ViewModels;
using ZenLoad.Views;

namespace ZenLoad;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = AppConfig.Load();
        var viewModel = new MainViewModel(
            config,
            FolderMonitorService.Instance,
            new WindowsFolderPickerService());
        var window = new MainWindow(viewModel);

        MainWindow = window;
        window.Show();

        if (e.Args.Any(argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase)))
        {
            window.HideToTray();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        FolderMonitorService.Instance.Dispose();
        base.OnExit(e);
    }
}
