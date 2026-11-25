using FFMpegDownloader.Models;

namespace FFMpegDownloader.Interfaces;

public interface IFFMpegService
{
    Task<string?> GetVersionAsync();

    Task<bool> InstallAsync();

    Task<bool> RunAsync(DownloadRequest download,
                        Action<string> outputNotifier,
                        IProgress<TimeSpan> currentTimeNotifier,
                        IProgress<TimeSpan> totalTimeNotifier,
                        IProgress<double> progressNotifier,
                        CancellationToken cancellationToken = default);
}
