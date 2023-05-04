using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoPlatform.Logger;

namespace VideoPlatform.YouTube;

public class YouTubeVideoService : IVideoService
{
    public string ServiceBaseUrl => "https://www.youtube.com/";
    private readonly HttpClient _client;
    private readonly IDictionary<string, string> _headers;
    private const string YouTubeVideoBaseUrl = "https://www.youtube.com/watch?v=";
    private const string YouTubeVideoLocalPath = "/watch";
    private const string YouTubeShortsLocalPath = "/shorts/";
    private const string YouTubeShortHost = "youtu.be/";
    private const string YouTubeHostUrl = "https://www.youtube.com";
    private const string StreamingData = "streamingData\":";
    private const string AdaptiveFormats = "adaptiveFormats\":";
    private readonly Regex _baseJsRegex = new(@"/[\w\/\d\.]+base.js", RegexOptions.Compiled);
    private const int HdVideoResolution = 1280*720;
    private readonly ILogger _logger;

    public YouTubeVideoService(HttpClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
        _headers = new Dictionary<string, string>
        {
            ["User-Agent"] = "PostmanRuntime/7.32.2",
            ["Accept"] = "*/*",
            ["Referer"] = ServiceBaseUrl
        };
    }
    
    private static string GetVideoId(string link, string pathSeparator, string queryParamsSeparator)
    {
        var idWithArguments = link.Split(pathSeparator).Last();
        var id = idWithArguments.Split(queryParamsSeparator).First();
        return id;
    }

    private static string GetVideoIdFromYouTubeShorts(string shortsLink)
    {
        const string queryParams = "?";
        return GetVideoId(shortsLink, YouTubeShortsLocalPath, queryParams);
    }

    private static string GetVideoIdFromYouTubeVideoWithShortLink(string videoLink)
    {
        const string queryParams = "?";
        return GetVideoId(videoLink, YouTubeShortHost, queryParams);
    }

    private static string GetVideoIdFromYouTubeVideo(string videoLink)
    {
        const string videoIdArgument = "v=";
        const string queryParamsSeparator = "&";
        return GetVideoId(videoLink, videoIdArgument, queryParamsSeparator);
    }

    private static string GetVideoId(Uri url)
    {
        var videoUrl = url.ToString();
        if (videoUrl.Contains(YouTubeVideoLocalPath))
        {
            return GetVideoIdFromYouTubeVideo(videoUrl);
        }

        if (videoUrl.Contains(YouTubeShortHost))
        {
            return GetVideoIdFromYouTubeVideoWithShortLink(videoUrl);
        }

        if (videoUrl.Contains(YouTubeShortsLocalPath))
        {
            return GetVideoIdFromYouTubeShorts(videoUrl);
        }

        throw new ArgumentException("Don't find any link to video or shorts", nameof(url));
    }
    
    private static string ExtractVideoInformationJson(string formats)
    {
        var videoFormatsJson = new StringBuilder();
        var brackets = new Stack<char>();
        var formatsWithoutSpaces = formats.Where(symbol => symbol != ' ');
        foreach (var symbol in formatsWithoutSpaces)
        {
            switch (symbol)
            {
                case '{' or '[':
                    brackets.Push(symbol);
                    break;
                case '}' or ']':
                    if (!brackets.Any())
                    {
                        throw new InvalidOperationException(
                            "Brackets sequence in JSON object is incorrect, not enough brackets");
                    }

                    var lastOpenedBracket = brackets.Pop();
                    if (!(lastOpenedBracket == '{' && symbol == '}' || lastOpenedBracket == '[' && symbol == ']'))
                    {
                        throw new InvalidOperationException(
                            "Brackets sequence in JSON object is incorrect, some brackets missed");
                    }

                    break;
            }
            
            videoFormatsJson.Append(symbol);
            if (!brackets.Any())
            {
                break;
            }
        }

        return videoFormatsJson.ToString();
    }
    
    private static IEnumerable<YouTubeVideoInfo> DeserializeYouTubeStream(string videoHtml)
    {
        var formats = videoHtml.Split(StreamingData).Last().Split(AdaptiveFormats).Last();
        var videoFormatsJson = ExtractVideoInformationJson(formats);
        var videos = JsonSerializer.Deserialize<List<YouTubeVideoInfo>>(videoFormatsJson)!;
        return videos.Where(e => e.IsVideo);
    }

    private async Task<string> ConvertUrlToSourceLink(string url)
    {
        using var client = new HttpClient();
        var videoHtml = await client.GetStringAsync(url);
        var videos = DeserializeYouTubeStream(videoHtml);
        var video = videos
            .OrderByDescending(video => video.Resolution)
            .MinBy(video => Math.Abs(video.Resolution - HdVideoResolution))!;
        if (video.IsUrlEncoded)
        {
            var baseJsPath = _baseJsRegex.Match(videoHtml).Value;
            var baseJsUrl = $"{YouTubeHostUrl}{baseJsPath}";
            var baseJs = await client.GetStringAsync(baseJsUrl);
            video.DecodeSignature(baseJs);
        }

        return video.Url!;
    }

    public async Task<Uri> DecodeUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var videoUrl = $"{YouTubeVideoBaseUrl}{GetVideoId(url)}";
        var sourceLink = await ConvertUrlToSourceLink(videoUrl);
        return new Uri(sourceLink);
    }

    public async Task<string> RefreshCookiesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ServiceBaseUrl);
        var response = await _client.SendAsync(request, cancellationToken);
        var headersForCookie = response.Headers.Where(header => header.Key.Equals("Set-Cookie")).SelectMany(header => header.Value);
        return string.Join(";", headersForCookie);
    }
}