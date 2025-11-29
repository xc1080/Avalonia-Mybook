using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MyBook.Services;
using MyBook.ViewModels;
using MyBook.Views;

namespace MyBook;

public class EditorApp
{
    public static void StartEditor(string[] args)
    {
        Program.BuildAvaloniaApp()
            .AfterSetup(_ =>
            {
                Console.WriteLine("编辑器配置完成");
            })
            .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
    }
}

public class EditorApplication : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataService = new SqliteStoryDataService("Stories");
            var viewModel = new StoryEditorViewModel(dataService);
            
            desktop.MainWindow = new StoryEditorWindow
            {
                DataContext = viewModel
            };
            
            _ = viewModel.InitializeCommand.ExecuteAsync(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
