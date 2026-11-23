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
    private readonly StringBuilder ConsoleBuffer = new();

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
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Pending;

    [ObservableProperty]
    public partial string ConsoleOutput { get; set; } = string.Empty;

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

    #endregion

    #region Notifieurs

    public readonly IProgress<double> ProgressNotifier;

    public readonly IProgress<TimeSpan> CurrentTimeNotifier;

    public readonly IProgress<TimeSpan> TotalTimeNotifier;

    #endregion

    #region Méthodes

    public void SetDownloading()
    {
        Status = DownloadStatus.Downloading;
        Progress = 0;
        ReportConsole($">>> Téléchargement : {Url}");
    }

    public void SetPaused()
    {
        Status = DownloadStatus.Paused;
        ReportConsole(">>> Téléchargement interrompu.");
    }

    public void SetFailed()
    {
        Status = DownloadStatus.Failed;
        ReportConsole($">>> Échec du téléchargement.");
    }

    public void SetFailed(string message)
    {
        Status = DownloadStatus.Failed;
        ReportConsole($">>> Échec du téléchargement : {message}.");
    }

    public void SetCompleted()
    {
        Status = DownloadStatus.Completed;
        ReportConsole(">>> Téléchargement terminé.");
    }

    public DownloadRequest GetRequest() => new(Url, OutputPath, Options);

    public void ReportConsole(string message)
    {
        lock (ConsoleLock)
        {
            ConsoleBuffer.AppendLine(message);
        }
    }

    public void FlushConsoleBuffer()
    {
        lock (ConsoleLock)
        {
            if (ConsoleBuffer.Length == 0)
                return;

            ConsoleOutput += ConsoleBuffer.ToString();
            ConsoleBuffer.Clear();
        }
    }

    #endregion

}
