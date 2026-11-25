using FFMpegDownloader.Models;

namespace FFMpegDownloader.Interfaces;

public interface ICurlParserService
{
    CurlRequest Parse(string command);
}
