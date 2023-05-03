using System.Runtime.Serialization;

namespace VideoPlatform;

public class UrlNotFoundException : Exception
{
    public UrlNotFoundException()
    {
    }

    protected UrlNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public UrlNotFoundException(string? message) : base(message)
    {
    }

    public UrlNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}