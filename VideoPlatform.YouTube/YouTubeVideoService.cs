using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoPlatform.Logger;

namespace VideoPlatform.YouTube;

public class YouTubeVideoService : IVideoService
{
    private const string YouTubeVideoBaseUrl = "https://www.youtube.com/watch?v=";
    private const string YouTubeVideoLocalPath = "/watch";
    private const string YouTubeShortsLocalPath = "/shorts/";
    private const string YouTubeShortHost = "youtu.be/";
    private const string YouTubeHostUrl = "https://www.youtube.com";
    private const string StreamingData = "streamingData\":";
    private const string AdaptiveFormats = "adaptiveFormats\":";
    private const int HdVideoResolution = 1280 * 720;
    private readonly Regex _baseJsRegex = new(@"/[\w\/\d\.]+base.js", RegexOptions.Compiled);
    private readonly HttpClient _client;
    private readonly IDictionary<string, string> _headers;
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

    public string ServiceBaseUrl => "https://www.youtube.com/";

    public async Task<Uri> DecodeUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        _logger.LogMessage($"Link: {url} requested to decode");
        var videoId = GetVideoId(url);
        var videoUrl = $"{YouTubeVideoBaseUrl}{videoId}";
        _logger.LogMessage($"Link: {url} video id is {videoId}");
        HttpResponseMessage response;
        string sourceLink;
        var retriesCount = 0;
        do
        {
            sourceLink = await ConvertUrlToSourceLink(videoUrl);
            using var request = new HttpRequestMessage(HttpMethod.Head, sourceLink);
            response = await _client.SendAsync(request, cancellationToken);
            ++retriesCount;
        } while (!response.IsSuccessStatusCode);

        _logger.LogMessage($"Retries count to decode url: {retriesCount}");
        return new Uri(sourceLink);
    }

    public async Task<string> RefreshCookiesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ServiceBaseUrl);
        var response = await _client.SendAsync(request, cancellationToken);
        var headersForCookie = response.Headers.Where(header => header.Key.Equals("Set-Cookie"))
            .SelectMany(header => header.Value);
        return string.Join(";", headersForCookie);
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
                    if (!((lastOpenedBracket == '{' && symbol == '}') || (lastOpenedBracket == '[' && symbol == ']')))
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
        var videoHtml = await _client.GetStringAsync(url);
        _logger.LogMessage($"Link: {url} html page requested, HTML:\n\n\n\n\n{videoHtml}\n\n\n\n\n");
        var videos = DeserializeYouTubeStream(videoHtml).ToList();
        _logger.LogMessage(
            $"By link: {url} found {videos.Count} videos, and links for each videos:\n{string.Join("\n", videos.Select(v => v.Url ?? v.SignatureCipher))}");
        var video = videos
            .OrderByDescending(video => video.Resolution)
            .MinBy(video => Math.Abs(video.Resolution - HdVideoResolution))!;
        _logger.LogMessage($"By link: {url} found HD video by link: {video.Url}");
        if (video.IsUrlEncoded)
        {
            _logger.LogMessage($"HD video by link: {url} is encoded");
            var baseJsPath = _baseJsRegex.Match(videoHtml).Value;
            _logger.LogMessage($"Base JS file from link: {url} found with path: {baseJsPath}");
            var baseJsUrl = $"{YouTubeHostUrl}{baseJsPath}";
            _logger.LogMessage($"Base JS file url: {baseJsUrl} found from link: {url}");
            var baseJs = await _client.GetStringAsync(baseJsUrl);
            video.DecodeSignature(baseJs, _logger);
        }

        return video.Url!;
    }
}