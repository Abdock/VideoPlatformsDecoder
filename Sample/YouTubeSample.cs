using VideoPlatform.Logger.Console;
using VideoPlatform.YouTube;

namespace Sample;

public static class YouTubeSample
{
    public static async Task RunAsync(string link)
    {
        using var client = new HttpClient();
        var logger = new ConsoleLogger();
        var youtube = new YouTubeVideoService(client, logger);
        var url = new Uri(link);
        var sourceLink = await youtube.DecodeUrlAsync(url);
        using var request = new HttpRequestMessage(HttpMethod.Head, sourceLink);
        var response = await client.SendAsync(request);
        Console.WriteLine($"YouTube: {response.StatusCode}");
    }
}