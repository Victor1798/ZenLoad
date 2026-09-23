using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;
using ZenLoad.Models;
using ZenLoad.Services;
using ZenLoad.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ZenLoad.Views;

public partial class MainWindow : FluentWindow
{
    private readonly ContentDialogService _dialogService = new();
    private readonly FolderMonitorService _monitorService = FolderMonitorService.Instance;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _pauseMenuItem;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _dialogService.SetDialogHost(RootContentDialogHost);

        SystemThemeWatcher.Watch(this);

        _pauseMenuItem = new Forms.ToolStripMenuItem();
        _pauseMenuItem.Click += OnPauseFromTrayClick;

        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Abrir ZenLoad", null, OnOpenFromTrayClick);
        trayMenu.Items.Add(_pauseMenuItem);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Salir", null, OnExitFromTrayClick);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = GetApplicationIcon(),
            Text = "ZenLoad",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _trayIcon.DoubleClick += OnTrayIconDoubleClick;

        Loaded += OnLoaded;
        Closing += OnClosing;
        UpdateTrayMenu();
        _monitorService.ActivityRecorded += OnActivityRecorded;
        _monitorService.StateChanged += OnMonitorStateChanged;
    }

    private static System.Drawing.Icon GetApplicationIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                return System.Drawing.Icon.ExtractAssociatedIcon(executablePath)
                    ?? System.Drawing.SystemIcons.Application;
            }
        }
        catch (Exception)
        {
            // Fall back to the Windows default icon if the executable icon cannot be read.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _monitorService.Start(((MainViewModel)DataContext).Config, ShowNewExtensionDialogAsync);
        UpdateTrayMenu();
    }

    public void HideToTray() => Hide();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnTrayIconDoubleClick(object? sender, EventArgs e) => ShowFromTray();

    private void OnOpenFromTrayClick(object? sender, EventArgs e) => ShowFromTray();

    private void OnPauseFromTrayClick(object? sender, EventArgs e)
    {
        ((MainViewModel)DataContext).TogglePauseCommand.Execute(null);
        UpdateTrayMenu();
    }

    private void OnExitFromTrayClick(object? sender, EventArgs e)
    {
        _allowClose = true;
        _monitorService.ActivityRecorded -= OnActivityRecorded;
        _monitorService.StateChanged -= OnMonitorStateChanged;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void UpdateTrayMenu()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateTrayMenu);
            return;
        }

        _pauseMenuItem.Text = ((MainViewModel)DataContext).IsPaused ? "Reanudar" : "Pausar";
    }

    private void OnMonitorStateChanged(object? sender, EventArgs e) => UpdateTrayMenu();

    private void OnActivityRecorded(object? sender, ActivityEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.Status is ActivityStatus.Moved or ActivityStatus.Error)
            {
                var icon = e.Status == ActivityStatus.Error
                    ? Forms.ToolTipIcon.Error
                    : Forms.ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(3500, "ZenLoad", e.Details, icon);
            }
        });
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
