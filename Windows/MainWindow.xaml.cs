using FFMpegDownloader.Interfaces;
using FFMpegDownloader.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FFMpegDownloader.Windows;

public sealed partial class MainWindow : FluentWindow
{

    public MainWindow(MainViewModel mainViewModel,
                      IFFMpegService ffmpegService,
                      IContentDialogService dialogService)
    {
        InitializeComponent();

        MainViewModel = mainViewModel;
        FFMpegService = ffmpegService;
        DialogService = dialogService;

        // Pour masquer le curseur dans la console, on définit la couleur du caret sur Transparent.
        FFMpegConsole.TextArea.Caret.CaretBrush = Brushes.Transparent;
        FFMpegConsole.Document.UndoStack.SizeLimit = 0;

        MainViewModel.SelectedDownloadChanged += OnSelectedDownloadChange;

        DataContext = MainViewModel;
        DialogService.SetDialogHost(RootContentDialogHost);
        Closing += OnClosing;
    }

    #region Champs

    private readonly MainViewModel MainViewModel;
    private readonly IFFMpegService FFMpegService;
    private readonly IContentDialogService DialogService;

    #endregion

    #region Propriétés calculées

    private bool IsFFMpegConsoleScrolledToEnd =>
        FFMpegConsole.VerticalOffset >= FFMpegConsole.ExtentHeight - FFMpegConsole.ViewportHeight - 1;

    private bool IsFFMpegConsoleSelectionActive =>
        FFMpegConsole.SelectionLength > 0;

    private bool IsFFMpegConsoleCollapsed => FFMpegConsole.ActualWidth == 0;

    #endregion

    public async Task InitializeAsync()
    {
        string? version = await FFMpegService.GetVersionAsync();
        if (version is not null)
            return;

        ContentDialog dialog = new()
        {
            Title = "FFMpeg manquant",
            Content =
            """
            FFMpeg n'a pas été détecté sur votre ordinateur.
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
        bool installed = await FFMpegService.InstallAsync();
        IsEnabled = true;

        if (installed)
        {
            dialog = new()
            {
                Title = "Installation terminée",
                Content =
                """
                FFMpeg a été installé.
                
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
                Content = "L'installation de FFMpeg a échoué.",
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
            bool scrollToEnd = !IsFFMpegConsoleSelectionActive && IsFFMpegConsoleScrolledToEnd;

            FFMpegConsole.AppendText(message);

            if (scrollToEnd)
                FFMpegConsole.ScrollToEnd(); // Fait défiler automatiquement vers le bas si l'utilisateur est déjà en bas du TextBox
        });
    }

    private void OnSelectedDownloadChange(DownloadItemViewModel? oldDownload, DownloadItemViewModel? newDownload)
    {
        if (oldDownload is not null)
        {
            oldDownload.UnsubscribeConsole();
            oldDownload.HistoryVerticalOffset = FFMpegConsole.VerticalOffset;
        }

        if (newDownload is null)
        {
            FFMpegConsole.Clear();
            return;
        }

        FFMpegConsole.Text = newDownload.SubscribeConsole(OnConsoleTextReceived);
        if (newDownload.IsDownloading)
            FFMpegConsole.ScrollToEnd();
        else
            FFMpegConsole.ScrollToVerticalOffset(newDownload.HistoryVerticalOffset);
    }

    private void OnScrollConsoleToEndClick(object sender, RoutedEventArgs e)
    {
        FFMpegConsole.ScrollToEnd();
    }

    private void OnConsoleSplitterPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        e.Handled = true;

        ConsoleColumn.Width = new GridLength(IsFFMpegConsoleCollapsed ? ActualWidth / 3 : 0);
    }

    private void OnFFMpegConsoleScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        MainViewModel.IsScrollConsoleToEndButtonVisible = !IsFFMpegConsoleScrolledToEnd;
    }
}
