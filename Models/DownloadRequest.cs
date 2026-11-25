namespace FFMpegDownloader.Models;

public sealed record DownloadRequest(Uri Url,
                                     string OutputPath,
                                     DownloadOptions Options);
