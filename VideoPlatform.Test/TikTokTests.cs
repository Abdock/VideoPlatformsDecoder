using System.Net;
using VideoPlatform.TikTok;

namespace VideoPlatform.Test;

public class TikTokTests
{
    private HttpClient _client = null!;
    
    [OneTimeSetUp]
    public void Initialize()
    {
        _client = new HttpClient();
    }
    
    [TestCase("https://vm.tiktok.com/ZMYKLCMJm/")]
    [TestCase("https://www.tiktok.com/@106sm/video/7224117086312860933?is_from_webapp=1&sender_device=pc&web_id=7207752210184209926")]
    [TestCase("https://www.tiktok.com/@106sm/video/7224117086312860933")]
    public async Task DecodeUrlAsync_TryDecodeCorrectTikTokUrl_HeadRequestReturnsOk(string link)
    {
        //Arrange
        const HttpStatusCode expectedResult = HttpStatusCode.OK;
        IVideoService tiktok = new TikTokVideoService(_client);
        var sourceLink = await tiktok.DecodeUrlAsync(new Uri(link));
        var cookies = await tiktok.RefreshCookiesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Head, sourceLink);
        request.Headers.Add("Cookie", cookies);
        request.Headers.Add("Referer", tiktok.ServiceBaseUrl);
        //Act
        var response = await _client.SendAsync(request);
        //Assert
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            await TestContext.Out.WriteLineAsync(sourceLink.ToString());
        }
        
        Assert.That(response.StatusCode, Is.EqualTo(expectedResult));
    }
}