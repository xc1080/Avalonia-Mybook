using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyBook.Services;
using MyBook.ViewModels;

namespace MyBook.Views;

public partial class PublishedLauncherWindow : Window
{
    private readonly string _novelName;

    public PublishedLauncherWindow() : this("视觉小说")
    {
    }

    public PublishedLauncherWindow(string novelName)
    {
        InitializeComponent();
        _novelName = novelName;
        
        // 设置窗口标题
        Title = novelName;
        TitleText.Text = novelName;
    }

    private async void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        var dataService = new SqliteStoryDataService("Stories");
        await dataService.InitializeDatabaseAsync();
        var chapters = await dataService.GetChaptersAsync();
        
        if (chapters.Count == 0)
        {
            await ShowErrorDialog("没有可用的故事章节。");
            return;
        }

        // 如果只有一个章节，直接开始
        if (chapters.Count == 1)
        {
            await StartGame(dataService, chapters[0]);
            return;
        }

        // 多个章节，让用户选择
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

        listBox.SelectionChanged += (s, e) => 
        {
            okButton.IsEnabled = listBox.SelectedItem != null;
        };

        okButton.Click += (s, e) => dialog.Close(listBox.SelectedItem);

        panel.Children.Add(listBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        var selected = await dialog.ShowDialog<object>(this);
        if (selected is MyBook.Models.Chapter chapter)
        {
            await StartGame(dataService, chapter);
        }
    }

    private async Task StartGame(SqliteStoryDataService dataService, MyBook.Models.Chapter chapter)
    {
        var gameViewModel = new StoryViewModelRuntime(dataService);
        await gameViewModel.InitializeAsync();
        await gameViewModel.LoadChapterAsync(chapter.Id);

        var gameWindow = new MainWindow
        {
            DataContext = gameViewModel,
            Title = _novelName
        };

        // 监听返回主菜单事件
        gameViewModel.ReturnToLauncherRequested += () =>
        {
            var newLauncher = new PublishedLauncherWindow(_novelName);
            newLauncher.Show();
            gameWindow.Close();
        };

        gameWindow.Show();
        this.Close();
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new Window
        {
            Title = "提示",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(25, 20),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 14,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.Black,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        var button = new Button
        {
            Content = "确定",
            Width = 100,
            Height = 36,
            Margin = new Avalonia.Thickness(0, 18, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            Foreground = Avalonia.Media.Brushes.White
        };
        button.Click += (s, e) => dialog.Close();

        panel.Children.Add(button);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
