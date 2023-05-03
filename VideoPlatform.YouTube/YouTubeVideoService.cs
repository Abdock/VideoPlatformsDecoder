namespace VideoPlatform.YouTube;

public class YouTubeVideoService : IVideoService
{
    public string ServiceBaseUrl => "https://www.youtube.com/";

    public async Task<Uri> DecodeUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<string> RefreshCookiesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}