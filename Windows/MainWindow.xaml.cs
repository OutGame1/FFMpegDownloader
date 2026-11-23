using FFmpegDownloader.Enums;
using FFmpegDownloader.Interfaces;
using FFmpegDownloader.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FFmpegDownloader.Windows;

public sealed partial class MainWindow : FluentWindow
{
    private readonly MainViewModel MainViewModel;
    private readonly IFFmpegService FFmpegService;
    private readonly IContentDialogService DialogService;

    public MainWindow(MainViewModel mainViewModel,
                      IFFmpegService ffmpegService,
                      IContentDialogService dialogService)
    {
        InitializeComponent();

        MainViewModel = mainViewModel;
        FFmpegService = ffmpegService;
        DialogService = dialogService;

        FFmpegConsole.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Border)
                return;

            DownloadItemViewModel? download = MainViewModel.SelectedDownload;
            if (download is not null && download.Status == DownloadStatus.Downloading)
                e.Handled = true; // Empêche la sélection du texte dans le TextBox lorsque le téléchargement est en cours
        };

        FFmpegConsole.TextChanged += (_, e) =>
        {
            if (FFmpegConsole.VerticalOffset >= FFmpegConsole.ExtentHeight - FFmpegConsole.ViewportHeight - 1)
                FFmpegConsole.ScrollToEnd(); // Fait défiler automatiquement vers le bas si l'utilisateur est déjà en bas du TextBox
        };

        DataContext = MainViewModel;
        DialogService.SetDialogHost(RootContentDialogHost);
        Closing += OnClosing;
    }

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
}
