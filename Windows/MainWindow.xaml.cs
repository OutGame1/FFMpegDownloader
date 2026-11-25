using FFmpegDownloader.Interfaces;
using FFmpegDownloader.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FFmpegDownloader.Windows;

public sealed partial class MainWindow : FluentWindow
{

    public MainWindow(MainViewModel mainViewModel,
                      IFFmpegService ffmpegService,
                      IContentDialogService dialogService)
    {
        InitializeComponent();

        MainViewModel = mainViewModel;
        FFmpegService = ffmpegService;
        DialogService = dialogService;

        MainViewModel.SelectedDownloadChanged += OnSelectedDownloadChange;

        DataContext = MainViewModel;
        DialogService.SetDialogHost(RootContentDialogHost);
        Closing += OnClosing;
    }

    #region Champs

    private readonly MainViewModel MainViewModel;
    private readonly IFFmpegService FFmpegService;
    private readonly IContentDialogService DialogService;

    #endregion

    #region Propriétés calculées

    private bool IsFFmpegConsoleScrolledToEnd =>
        FFmpegConsole.VerticalOffset >= FFmpegConsole.ExtentHeight - FFmpegConsole.ViewportHeight - 1;

    #endregion

    public async Task InitializeAsync()
    {
        string? version = await FFmpegService.GetVersionAsync();
        if (version is not null)
            return;

        ContentDialog dialog = new()
        {
            Title = "FFmpeg manquant",
            Content =
            """
            FFmpeg n'a pas été détecté sur votre ordinateur.
            Ce logiciel est nécessaire pour que l'application fonctionne correctement.
            
            Voulez-vous l'installer automatiquement ?
            """,
            PrimaryButtonText = "Oui",
            CloseButtonText = "Non, fermer l'application",
            PrimaryButtonAppearance = ControlAppearance.Success,
            CloseButtonAppearance = ControlAppearance.Danger,
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await DialogService.ShowAsync(dialog, CancellationToken.None);

        if (result != ContentDialogResult.Primary)
        {
            Application.Current.Shutdown();
            return;
        }

        IsEnabled = false;
        bool installed = await FFmpegService.InstallAsync();
        IsEnabled = true;

        if (installed)
        {
            dialog = new()
            {
                Title = "Installation terminée",
                Content =
                """
                FFmpeg a été installé.
                
                Veuillez redémarrer l'application pour actualiser les variables d'environnement.
                """,
                CloseButtonText = "OK",
                CloseButtonAppearance = ControlAppearance.Info,
                DefaultButton = ContentDialogButton.Close
            };
        }
        else
        {
            dialog = new()
            {
                Title = "Erreur",
                Content = "L'installation de FFmpeg a échoué.",
                CloseButtonText = "OK",
                CloseButtonAppearance = ControlAppearance.Info,
                DefaultButton = ContentDialogButton.Close
            };
        }

        await DialogService.ShowAsync(dialog, CancellationToken.None);

        Application.Current.Shutdown();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;

        Closing -= OnClosing;

        try
        {
            await MainViewModel.StopQueueAsync();
        }
        finally
        {
            await Dispatcher.InvokeAsync(Close);
        }
    }

    private void OnConsoleTextReceived(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            bool wasAtBottom = IsFFmpegConsoleScrolledToEnd;

            FFmpegConsole.AppendText(message);

            if (wasAtBottom)
                FFmpegConsole.ScrollToEnd(); // Fait défiler automatiquement vers le bas si l'utilisateur est déjà en bas du TextBox
        });
    }

    private void OnSelectedDownloadChange(DownloadItemViewModel? oldDownload, DownloadItemViewModel? newDownload)
    {
        if (oldDownload is not null)
        {
            oldDownload.UnsubscribeConsole();
            oldDownload.HistoryVerticalOffset = FFmpegConsole.VerticalOffset;
        }

        if (newDownload is null)
        {
            FFmpegConsole.Clear();
            return;
        }

        FFmpegConsole.Text = newDownload.SubscribeConsole(OnConsoleTextReceived);
        if (newDownload.IsDownloading)
            FFmpegConsole.ScrollToEnd();
        else
            FFmpegConsole.ScrollToVerticalOffset(newDownload.HistoryVerticalOffset);
    }
}
