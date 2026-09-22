using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using ZenLoad.Services;
using ZenLoad.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ZenLoad.Views;

public partial class MainWindow : FluentWindow
{
    private readonly ContentDialogService _dialogService = new();
    private readonly FolderMonitorService _monitorService = FolderMonitorService.Instance;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _dialogService.SetDialogHost(RootContentDialogHost);
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _monitorService.Start(((MainViewModel)DataContext).Config, ShowNewExtensionDialogAsync);
    }

    public void HideToTray()
    {
        Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnTrayIconDoubleClick(object sender, RoutedEventArgs e) => ShowFromTray();

    private void OnOpenFromTrayClick(object sender, RoutedEventArgs e) => ShowFromTray();

    private void OnExitFromTrayClick(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        TrayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async Task<bool> ShowNewExtensionDialogAsync(string extension)
    {
        var showDialog = new Func<Task<bool>>(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Nueva extensión detectada",
                Content = $"Se ha detectado una nueva extensión: {extension}. ¿Deseas crear una nueva carpeta en Descargas con este nombre y organizar futuros archivos aquí?",
                PrimaryButtonText = "Crear carpeta",
                SecondaryButtonText = "Ignorar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await _dialogService.ShowAsync(dialog, CancellationToken.None);
            return result == ContentDialogResult.Primary;
        });

        if (Dispatcher.CheckAccess())
        {
            return await showDialog();
        }

        return await Dispatcher.InvokeAsync(showDialog).Task.Unwrap();
    }
}
