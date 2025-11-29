using Avalonia;
using System;
using System.Linq;

namespace MyBook;

sealed class Program
{
    
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--editor") || args.Contains("-e"))
        {
            Console.WriteLine("正在启动编辑器...");
            App.IsEditorMode = true;
        }
        else
        {
            Console.WriteLine("正在启动游戏...");
            App.IsEditorMode = false;
        }
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}