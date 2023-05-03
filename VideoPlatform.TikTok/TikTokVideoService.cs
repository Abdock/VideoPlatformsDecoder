using System.Text.RegularExpressions;

namespace VideoPlatform.TikTok;

public partial class TikTokVideoService : IVideoService
{
    private readonly HttpClient _client;
    public string ServiceBaseUrl => "https://www.tiktok.com/";

    private readonly IDictionary<string, string> _headers = new Dictionary<string, string>
    {
        ["User-Agent"] = "PostmanRuntime/7.32.2",
        ["Accept"] = "*/*",
    };


    public TikTokVideoService(HttpClient client)
    {
        _client = client;
        _headers.Add("Referer", ServiceBaseUrl);
    }

    public async Task<string> RefreshCookiesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ServiceBaseUrl);
        var response = await _client.SendAsync(request, cancellationToken);
        var headersForCookie = response.Headers.Where(header => header.Key.Equals("Set-Cookie"))
            .SelectMany(header => header.Value);
        return string.Join(";", headersForCookie);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, Uri url,
        CancellationToken cancellationToken = default)
    {
        var cookies = await RefreshCookiesAsync(cancellationToken);
        _headers.Add("Cookie", cookies);
        var request = new HttpRequestMessage(method, url);
        foreach (var (name, value) in _headers)
        {
            request.Headers.Add(name, value);
        }

        return request;
    }

    public async Task<Uri> DecodeUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _client.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!TargetScriptRegex().IsMatch(html))
        {
            throw new UrlNotFoundException("HTML doesn't contains source URL to video");
        }
        
        var downloadAddressJsonProperty = DownloadSourceLinkRegex().Match(html).Value;
        var sourceLink = UrlRegex().Match(downloadAddressJsonProperty).Value;
        sourceLink = SlashRegex().Replace(sourceLink, "/");
        return new Uri(sourceLink);
    }

    [GeneratedRegex("\"downloadAddr\":.{1,1024}", RegexOptions.Compiled)]
    private static partial Regex DownloadSourceLinkRegex();

    [GeneratedRegex("https:[\\\\\\w\\-\\.\\?=\\&%~]+", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    [GeneratedRegex("<script id=\"SIGI_STATE\".*>", RegexOptions.Compiled)]
    private static partial Regex TargetScriptRegex();
    
    [GeneratedRegex(@"\\u002F", RegexOptions.Compiled)]
    private static partial Regex SlashRegex();
}