using FFMpegDownloader.Interfaces;

namespace FFMpegDownloader.Models;

public sealed record DownloadOptions
{
    public readonly IDictionary<string, string> Headers;

    public readonly string Title;

    public readonly string Artist;

    public readonly string Album;

    public readonly string Date;

    public readonly string Genre;

    public readonly string Comment;

    public DownloadOptions(IEnumerable<IHttpHeader> headers,
                           string title,
                           string artist,
                           string album,
                           string date,
                           string genre,
                           string comment)
    {
        Headers = headers.ToDictionary(header => header.Name,
                                       header => header.Value,
                                       StringComparer.OrdinalIgnoreCase);
        Title = title;
        Artist = artist;
        Album = album;
        Date = date;
        Genre = genre;
        Comment = comment;
    }
}
