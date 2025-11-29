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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
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