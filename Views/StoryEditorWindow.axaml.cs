using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MyBook.ViewModels;

namespace MyBook.Views;

public partial class StoryEditorWindow : Window
{
    public StoryEditorWindow()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachResourcePickerHandlers();
    }

    private void AttachResourcePickerHandlers()
    {
        if (DataContext is StoryEditorViewModel vm)
        {
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets");
            assetsPath = Path.GetFullPath(assetsPath);
            vm.RequestImageSelect = async callback =>
            {
                // 使用新的资源浏览器
                var picker = new ResourceBrowserWindow(
                    "选择图片资源", 
                    "图片",
                    new[] { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".webp" });
                var result = await picker.ShowDialog<ResourceBrowserResult?>(this);
                if (result != null && !string.IsNullOrWhiteSpace(result.RelativePath))
                {
                    callback(result.RelativePath);
                }
                else
                {
                    callback(null);
                }
            };
            vm.RequestAudioSelect = async callback =>
            {
                // 使用新的资源浏览器
                var picker = new ResourceBrowserWindow(
                    "选择音频资源",
                    "音频",
                    new[] { ".mp3", ".wav", ".ogg", ".flac" });
                var result = await picker.ShowDialog<ResourceBrowserResult?>(this);
                if (result != null && !string.IsNullOrWhiteSpace(result.RelativePath))
                {
                    callback(result.RelativePath);
                }
                else
                {
                    callback(null);
                }
            };
            

        }
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

    private async void OnCreateChapterClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "新建章节",
            Width = 400,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };

        var textBox = new TextBox
        {
            Watermark = "请输入章节标题...",
            Margin = new Avalonia.Thickness(20, 20, 20, 10),
            Background = Avalonia.Media.Brushes.White,
            Foreground = Avalonia.Media.Brushes.Black
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10,
            Margin = new Avalonia.Thickness(20, 10, 20, 20)
        };

        var okButton = new Button
        {
            Content = "确定",
            Width = 100,
            Background = Avalonia.Media.Brushes.Green,
            Foreground = Avalonia.Media.Brushes.White
        };
        okButton.Click += (s, args) => dialog.Close(textBox.Text);

        var cancelButton = new Button
        {
            Content = "取消",
            Width = 100
        };
        cancelButton.Click += (s, args) => dialog.Close(null);

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var panel = new StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;

        var result = await dialog.ShowDialog<string?>(this);
        
        if (!string.IsNullOrWhiteSpace(result) && DataContext is StoryEditorViewModel vm)
        {
            await vm.CreateChapterCommand.ExecuteAsync(result);
        }
    }

    private void OnReturnToMainMenuClicked(object? sender, RoutedEventArgs e)
    {
        // Open the launcher window and close the editor
        var launcher = new LauncherWindow();
        launcher.Show();
        this.Close();
    }
}
