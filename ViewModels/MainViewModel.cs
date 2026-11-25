using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFmpegDownloader.Enums;
using FFmpegDownloader.Interfaces;
using FFmpegDownloader.Models;
using FFmpegDownloader.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FFmpegDownloader.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{

    public MainViewModel(IServiceProvider serviceProvider,
                         IFFmpegService ffmpegService,
                         ICurlParserService curlParserService,
                         AdvancedOptionsViewModel advancedOptionsViewModel)
    {
        ServiceProvider = serviceProvider;
        FFmpegService = ffmpegService;
        CurlParserService = curlParserService;
        AdvancedOptionsViewModel = advancedOptionsViewModel;

        Downloads.CollectionChanged += (_, _) =>
        {
            StartQueueCommand.NotifyCanExecuteChanged();
            StopQueueCommand.NotifyCanExecuteChanged();
        };
    }

    #region Champs

    private readonly IServiceProvider ServiceProvider;
    private readonly IFFmpegService FFmpegService;
    private readonly ICurlParserService CurlParserService;
    private readonly AdvancedOptionsViewModel AdvancedOptionsViewModel;
    private CancellationTokenSource? QueueCancellation;
    private TaskCompletionSource? QueueCompletion;

    #endregion

    #region Propriétés observables

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = [];

    [ObservableProperty]
    public partial DownloadItemViewModel? SelectedDownload { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
    public partial string VideoUrl { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
    public partial string OutputPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartQueueCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopQueueCommand))]
    public partial bool IsQueueRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    public partial string LoadingMessage { get; set; } = string.Empty;

    #endregion

    #region Propriétés calculées

    public bool IsLoading => LoadingMessage.Length != 0;

    #endregion

    #region Événements

    public event Action<DownloadItemViewModel?, DownloadItemViewModel?>? SelectedDownloadChanged;

    partial void OnSelectedDownloadChanged(DownloadItemViewModel? oldValue, DownloadItemViewModel? newValue)
    {
        SelectedDownloadChanged?.Invoke(oldValue, newValue);
    }

    #endregion

    #region Commandes

    [RelayCommand]
    private void OpenAdvancedOptions()
    {
        AdvancedOptionsWindow window = ServiceProvider.GetRequiredService<AdvancedOptionsWindow>();
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(CanAddToQueue))]
    private void AddToQueue()
    {
        DownloadOptions options = AdvancedOptionsViewModel.CreateSnapshot();

        Downloads.Add(new DownloadItemViewModel(VideoUrl, OutputPath, options));

        VideoUrl = string.Empty;
        OutputPath = string.Empty;
        AdvancedOptionsViewModel.Reset();
    }

    private bool CanAddToQueue() =>
        Uri.TryCreate(VideoUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && OutputPath.Length > 0;

    [RelayCommand(CanExecute = nameof(CanStartQueue))]
    private async Task StartQueueAsync()
    {
        if (IsQueueRunning)
            return;

        IsQueueRunning = true;
        QueueCancellation = new CancellationTokenSource();
        QueueCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationToken cancellationToken = QueueCancellation.Token;

        try
        {
            // Utilisation d'une boucle for pour pouvoir ajouter
            // des téléchargements pendant l'exécution de la file.
            for (int i = 0; i < Downloads.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await Download(Downloads[i], cancellationToken);
            }
        }
        finally
        {
            QueueCancellation.Dispose();
            QueueCancellation = null;
            IsQueueRunning = false;
            QueueCompletion.TrySetResult();
            QueueCompletion = null;
        }
    }

    private async Task Download(DownloadItemViewModel download, CancellationToken cancellationToken)
    {
        if (download.Status != DownloadStatus.Pending)
            return;

            download.Status = DownloadStatus.Downloading;
            download.Progress = 0;
        SelectedDownload = download;

        try
        {
            bool success = await FFmpegService.RunAsync(download.GetRequest(),
                                                        download.ReportConsole,
                                                                download.CurrentTimeNotifier,
                                                                download.TotalTimeNotifier,
                                                                download.ProgressNotifier,
                                                                cancellationToken);

                    if (success)
                    {
                download.Status = DownloadStatus.Completed;
                    }
                    else if (cancellationToken.IsCancellationRequested)
                    {
                download.Status = DownloadStatus.Paused;
                    }
                    else
                    {
                download.Status = DownloadStatus.Failed;
                    }
                }
                catch (Exception exception)
                {
            download.Status = DownloadStatus.Failed;
            // TODO: Afficher un message d'erreur à l'utilisateur.
            Debug.WriteLine($"Erreur lors du téléchargement de {download.Url} : {exception.Message}");
        }
        }

    private bool CanStartQueue() => !IsQueueRunning && Downloads.Any(d => d.Status == DownloadStatus.Pending);

    [RelayCommand(CanExecute = nameof(IsQueueRunning))]
    public async Task StopQueueAsync()
    {
        if (!IsQueueRunning)
            return;

        try
        {
            LoadingMessage = "Arrêt de la file d'attente...";

            Task? completionTask = QueueCompletion?.Task;

            QueueCancellation?.Cancel();

            if (completionTask is not null)
                await completionTask;
        }
        finally
        {
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void BrowseOutputFile()
    {
        SaveFileDialog dialog = new()
        {
            Filter = "Matroska (*.mkv)|*.mkv|MP4 (*.mp4)|*.mp4",
            AddExtension = true,
            FileName = Path.GetFileName(OutputPath)
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ImportCurl()
    {
        CurlImportWindow window = ServiceProvider.GetRequiredService<CurlImportWindow>();
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() != true)
            return;

        try
        {
            CurlRequest request = CurlParserService.Parse(window.CommandText);

            VideoUrl = request.Url;

            AdvancedOptionsViewModel.Headers.Clear();
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                AdvancedOptionsViewModel.Headers.Add(
                    new HttpHeaderViewModel
                    {
                        Name = header.Key,
                        Value = header.Value
                    });
            }
        }
        catch (Exception exception)
        {
            // TODO: Afficher un message d'erreur à l'utilisateur.
            Debug.WriteLine(exception.Message);
        }
    }

    #endregion

}
