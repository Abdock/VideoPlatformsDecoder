namespace VideoPlatform.Logger;

public interface ILogger
{
    IReadOnlyList<string> Logs { get; }

    void LogMessage(string message);
}