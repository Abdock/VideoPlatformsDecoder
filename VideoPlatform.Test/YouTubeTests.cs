using System.Net;
using VideoPlatform.Logger.Console;
using VideoPlatform.YouTube;

namespace VideoPlatform.Test;

public class YouTubeTests
{
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Initialize()
    {
        _client = new HttpClient();
    }

    [TestCase("https://www.youtube.com/shorts/6tX2xK23Hk4")]
    [TestCase("https://www.youtube.com/shorts/9xJFuDi7Ir8")]
    [TestCase("https://www.youtube.com/watch?v=5EKquLnbo0k&list=RDMM5EKquLnbo0k&start_radio=1")]
    public async Task DecodeUrlAsync_TryDecodeCorrectYouTubeLinks_HeadRequestsReturnsOk(string link)
    {
        //Arrange
        const HttpStatusCode expectedResult = HttpStatusCode.OK;
        var logger = new ConsoleLogger();
        IVideoService youtube = new YouTubeVideoService(_client, logger);
        var sourceLink = await youtube.DecodeUrlAsync(new Uri(link));
        using var request = new HttpRequestMessage(HttpMethod.Head, sourceLink);
        //Act
        var response = await _client.SendAsync(request);
        //Assert
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            await TestContext.Out.WriteLineAsync(string.Join("\n", logger.Logs));
        }
        
        Assert.That(response.StatusCode, Is.EqualTo(expectedResult));
    }

    [OneTimeTearDown]
    public void Clear()
    {
        _client.Dispose();
    }
}