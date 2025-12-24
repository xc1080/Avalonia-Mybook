using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using MyBook.ViewModels;
using MyBook.Views;
using MyBook.Services;

namespace MyBook;

public partial class App : Application
{
    public static bool IsEditorMode { get; set; }
    public static bool IsPublishedVersion { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // 检查是否为发布版本
        IsPublishedVersion = PublishService.IsPublishedVersion();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            if (IsEditorMode)
            {
                var dataService = new SqliteStoryDataService("Stories");
                var viewModel = new StoryEditorViewModel(dataService);

                desktop.MainWindow = new StoryEditorWindow
                {
                    DataContext = viewModel
                };

                _ = viewModel.InitializeCommand.ExecuteAsync(null);
            }
            else if (IsPublishedVersion)
            {
                // 发布版本：创建简化的启动器或直接进入游戏
                var config = PublishService.GetPublishConfig();
                var includeEditor = config.TryGetValue("IncludeEditor", out var val) && val == "True";
                var novelName = config.TryGetValue("NovelName", out var name) ? name : "视觉小说";

                if (includeEditor)
                {
                    // 包含编辑器，显示启动器
                    desktop.MainWindow = new LauncherWindow
                    {
                        Title = novelName
                    };
                }
                else
                {
                    // 纯播放器模式，显示精简启动器
                    desktop.MainWindow = new PublishedLauncherWindow(novelName);
                }
            }
            else
            {
                desktop.MainWindow = new LauncherWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}