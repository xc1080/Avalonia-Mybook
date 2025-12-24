using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyBook.Services;
using MyBook.ViewModels;

namespace MyBook.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    private async void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        var dataService = new SqliteStoryDataService("Stories");
        await dataService.InitializeDatabaseAsync();
        var chapters = await dataService.GetChaptersAsync();
        if (chapters.Count == 0)
        {
            await ShowErrorDialog("没有可用章节，请先在编辑器中创建章节。");
            return;
        }
        var dialog = new Window
        {
            Title = "选择章节",
            Width = 400,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };
        
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "请选择要开始的章节：",
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brushes.Black,
            Margin = new Avalonia.Thickness(0, 0, 0, 15)
        });
        
        var listBox = new ListBox
        {
            ItemsSource = chapters,
            DisplayMemberBinding = new Avalonia.Data.Binding("Title"),
            Height = 180,
            Background = Avalonia.Media.Brushes.White,
            Foreground = Avalonia.Media.Brushes.Black
        };
        var okButton = new Button
        {
            Content = "开始游戏",
            Width = 120,
            Height = 38,
            Margin = new Avalonia.Thickness(0, 15, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            IsEnabled = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            Foreground = Avalonia.Media.Brushes.White
        };
        listBox.SelectionChanged += (s, e) => okButton.IsEnabled = listBox.SelectedItem != null;
        okButton.Click += (s, e) => dialog.Close(listBox.SelectedItem);
        panel.Children.Add(listBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;
        var selected = await dialog.ShowDialog<object>(this);
        if (selected is MyBook.Models.Chapter chapter)
        {
            var gameViewModel = new StoryViewModelRuntime(dataService);
            await gameViewModel.InitializeAsync();
            await gameViewModel.LoadChapterAsync(chapter.Id);
            var gameWindow = new MainWindow
            {
                DataContext = gameViewModel
            };
            gameWindow.Show();
            this.Close();
        }
    }

    private async void OnEditorClicked(object? sender, RoutedEventArgs e)
    {
        var dataService = new SqliteStoryDataService("Stories");
        var viewModel = new StoryEditorViewModel(dataService);

        var editorWindow = new StoryEditorWindow
        {
            DataContext = viewModel
        };

        await viewModel.InitializeCommand.ExecuteAsync(null);

        editorWindow.Show();
        this.Close();
    }
    private async Task ShowErrorDialog(string message)
    {
        var dlg = new Window
        {
            Title = "错误",
            Width = 400,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, Margin = new Avalonia.Thickness(0,0,0,15), Foreground = Avalonia.Media.Brushes.Red, FontSize = 14, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "确定", Width = 100, Height = 36, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")), Foreground = Avalonia.Media.Brushes.White }
                }
            }
        };
        ((Button)((StackPanel)dlg.Content).Children[1]).Click += (s, e) => dlg.Close();
        await dlg.ShowDialog(this);
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OnPublishClicked(object? sender, RoutedEventArgs e)
    {
        var publishWindow = new PublishWindow();
        publishWindow.ShowDialog(this);
    }
}
