namespace VideoPlatform.Logger.Console;

public class ConsoleLogger : ILogger
{
    private readonly IList<string> _logs;

    public ConsoleLogger()
    {
        _logs = new List<string>();
    }

    public IReadOnlyList<string> Logs => _logs.AsReadOnly();

    public void LogMessage(string message)
    {
        _logs.Add(message);
    }

    public void Clear()
    {
        _logs.Clear();
    }
}