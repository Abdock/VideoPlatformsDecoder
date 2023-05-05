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

    [TestCase("https://youtu.be/CL8ihD3rPC4")]
    [TestCase("https://www.youtube.com/shorts/6tX2xK23Hk4")]
    [TestCase("https://www.youtube.com/shorts/9xJFuDi7Ir8")]
    [TestCase("https://www.youtube.com/watch?v=LDhPs8BAFKk")]
    [TestCase("https://www.youtube.com/shorts/SqwM04cHNqY")]
    [TestCase("https://www.youtube.com/shorts/2ydko8o8SUE")]
    [TestCase("https://www.youtube.com/shorts/9g6Bh-tMPg8")]
    [TestCase("https://www.youtube.com/shorts/zKX5S_uw8AQ")]
    [TestCase("https://www.youtube.com/shorts/LfFLexL3ckE")]
    [TestCase("https://youtube.com/shorts/73xblB0i05U?feature=share")]
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
        Assert.That(response.StatusCode, Is.EqualTo(expectedResult));
    }

    [OneTimeTearDown]
    public void Clear()
    {
        _client.Dispose();
    }
}