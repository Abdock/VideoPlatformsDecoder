using VideoPlatform.TikTok;

namespace Sample;

public static class TikTokSample
{
    public static async Task RunAsync(string link)
    {
        using var client = new HttpClient();
        var tiktok = new TikTokVideoService(client);
        var url = new Uri(link);
        var sourceLink = await tiktok.DecodeUrlAsync(url);
        var cookies = await tiktok.RefreshCookiesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceLink);
        request.Headers.Add("Cookie", cookies);
        request.Headers.Add("Referer", tiktok.ServiceBaseUrl);
        var response = await client.SendAsync(request);
        Console.WriteLine($"TikTok {response.StatusCode}");
    }
}