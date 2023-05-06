namespace VideoPlatform;

public interface IVideoService
{
    string ServiceBaseUrl { get; }

    Task<Uri> DecodeUrlAsync(Uri url, CancellationToken cancellationToken = default);

    Task<string> RefreshCookiesAsync(CancellationToken cancellationToken = default);
}