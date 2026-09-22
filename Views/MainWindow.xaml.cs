using System.Windows;
using System.Windows.Threading;
using ZenLoad.Services;
using ZenLoad.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ZenLoad.Views;

public partial class MainWindow : FluentWindow
{
    private readonly ContentDialogService _dialogService = new();
    private readonly FolderMonitorService _monitorService = FolderMonitorService.Instance;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _dialogService.SetDialogHost(RootContentDialogHost);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _monitorService.Start(((MainViewModel)DataContext).Config, ShowNewExtensionDialogAsync);
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
