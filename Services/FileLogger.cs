namespace MyBook.Services;

using System;
using System.IO;

public static class FileLogger
{
    private static readonly string _logPath;

    static FileLogger()
    {
        try
        {
            var storiesDir = Path.Combine(AppContext.BaseDirectory, "Stories");
            if (!Directory.Exists(storiesDir)) Directory.CreateDirectory(storiesDir);
            _logPath = Path.Combine(storiesDir, "runtime.log");
        }
        catch
        {
            _logPath = Path.Combine(AppContext.BaseDirectory, "runtime.log");
        }
    }

    public static void Log(string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:o} {message}" + Environment.NewLine;
            File.AppendAllText(_logPath, line);
        }
        catch
        {
            // swallow logging errors
        }
    }
}
