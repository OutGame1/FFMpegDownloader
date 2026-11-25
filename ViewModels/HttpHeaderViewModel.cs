using CommunityToolkit.Mvvm.ComponentModel;
using FFMpegDownloader.Interfaces;

namespace FFMpegDownloader.ViewModels;

public sealed partial class HttpHeaderViewModel : ObservableObject, IHttpHeader
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}
