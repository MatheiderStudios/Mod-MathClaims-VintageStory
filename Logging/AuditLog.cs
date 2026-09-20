namespace MathClaims.Logging;
public sealed class AuditLog(string directory)
{
    private readonly string path = Path.Combine(directory, "audit.log");
    public void Write(string action, string detail)
    {
        Directory.CreateDirectory(directory);
        File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O}\t{action}\t{detail.Replace('\n', ' ')}{Environment.NewLine}");
    }
}
