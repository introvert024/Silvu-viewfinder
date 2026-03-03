using System;
using System.IO;
using System.Text;

static class Logger
{
    private static readonly object _lock = new object();
    private static readonly string _path = Path.Combine(AppContext.BaseDirectory, "Silvuviewfinder.log");

    public static void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(_path, line, Encoding.UTF8);
            }
        }
        catch { /* swallow — logging best-effort */ }
    }

    public static void LogException(Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EXCEPTION: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine("InnerException:");
                sb.AppendLine(ex.InnerException.ToString());
            }
            lock (_lock)
            {
                File.AppendAllText(_path, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { }
    }

    public static string LogPath => _path;
}
