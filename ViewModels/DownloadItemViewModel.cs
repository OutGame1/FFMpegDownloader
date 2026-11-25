using CommunityToolkit.Mvvm.ComponentModel;
using FFmpegDownloader.Enums;
using FFmpegDownloader.Models;
using System.IO;
using System.Text;

namespace FFmpegDownloader.ViewModels;

public partial class DownloadItemViewModel : ObservableObject
{

    public DownloadItemViewModel(string url,
                                 string outputPath,
                                 DownloadOptions options)
    {
        Url = new(url);
        OutputPath = outputPath;
        Options = options;

        CurrentTimeNotifier = new Progress<TimeSpan>(value => CurrentTime = value);
        TotalTimeNotifier = new Progress<TimeSpan>(value => TotalTime = value);
        ProgressNotifier = new Progress<double>(value => Progress = value);
    }

    #region Champs

    private readonly Lock ConsoleLock = new();
    private readonly StringBuilder ConsoleHistory = new();
    public double HistoryVerticalOffset;

    #endregion

    #region Propriétés observables

    public Uri Url { get; }

    public string OutputPath { get; }

    public DownloadOptions Options { get; }

    [ObservableProperty]
    public partial double Progress { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTime))]
    public partial TimeSpan CurrentTime { get; set; } = TimeSpan.Zero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTime))]
    public partial TimeSpan TotalTime { get; set; } = TimeSpan.Zero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusMessage))]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Pending;

    #endregion

    #region Propriétés calculées

    public string FormattedTime => $"{CurrentTime:hh\\:mm\\:ss} / {TotalTime:hh\\:mm\\:ss}";

    public string FileName => Path.GetFileName(OutputPath);

    public string StatusMessage => Status switch
    {
        DownloadStatus.Pending => "En attente",
        DownloadStatus.Downloading => "En cours",
        DownloadStatus.Completed => "Terminé",
        DownloadStatus.Paused => "En pause",
        DownloadStatus.Failed => "Échec",
        _ => "Inconnu"
    };

    public bool IsDownloading => Status == DownloadStatus.Downloading;

    #endregion

    #region Notifieurs

    public readonly IProgress<double> ProgressNotifier;

    public readonly IProgress<TimeSpan> CurrentTimeNotifier;

    public readonly IProgress<TimeSpan> TotalTimeNotifier;

    #endregion

    #region Événements

    public event Action<string>? ConsoleChanged;

    #endregion

    #region Méthodes

    public void ReportConsole(string message)
    {
        string line = message + Environment.NewLine;

        lock (ConsoleLock)
        {
            // Conserve le message dans l'historique.
            ConsoleHistory.Append(line);

            // Notifie le listener actuel.
            // L'appel est effectué sous verrou pour empêcher
            // un désabonnement simultané.
            ConsoleChanged?.Invoke(line);
        }
    }

    public string SubscribeConsole(Action<string> listener)
    {
        lock (ConsoleLock)
        {
            ConsoleChanged = listener;
            return ConsoleHistory.ToString();
        }
    }

    public void UnsubscribeConsole()
    {
        lock (ConsoleLock)
        {
            ConsoleChanged = null;
        }
    }

    public string GetConsole()
    {
        lock (ConsoleLock)
        {
            return ConsoleHistory.ToString();
        }
    }

    public DownloadRequest GetRequest() => new(Url, OutputPath, Options);

    #endregion

}
