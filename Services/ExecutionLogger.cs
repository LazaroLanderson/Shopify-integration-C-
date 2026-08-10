namespace ShopifyIntegration.Services;

public class ExecutionLogger
{
    private readonly string filePath;

    public ExecutionLogger(string filePath)
    {
        this.filePath = filePath;
    }

    public void Log(string message)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        File.AppendAllText(filePath, $"{DateTimeOffset.Now:O} - {message}{Environment.NewLine}");
    }
}