namespace VideoPlatform.Logger.Console;

public class ConsoleLogger : ILogger
{
    private readonly IList<string> _logs;
    public IReadOnlyList<string> Logs => _logs.AsReadOnly();

    public ConsoleLogger()
    {
        _logs = new List<string>();
    }
    
    public void LogMessage(string message)
    {
        _logs.Add(message);
    }
}