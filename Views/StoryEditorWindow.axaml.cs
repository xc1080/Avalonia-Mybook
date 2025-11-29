using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyBook.ViewModels;
using System.Threading.Tasks;

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
                if (!Directory.Exists(assetsPath))
                {
                    await ShowErrorDialog("资源目录不存在: " + assetsPath);
                    callback(null);
                    return;
                }

                var resources = Directory.GetFiles(assetsPath)
                    .Select(Path.GetFileName)
                    .Where(f => f != null && (f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".bmp") || f.EndsWith(".ico")))
                    .Cast<string>();
                var picker = new ResourcePickerWindow("选择图片资源", resources);
                var result = await picker.ShowDialog<string?>(this);
                callback(result);
            };
            vm.RequestAudioSelect = async callback =>
            {
                if (!Directory.Exists(assetsPath))
                {
                    await ShowErrorDialog("资源目录不存在: " + assetsPath);
                    callback(null);
                    return;
                }

                var resources = Directory.GetFiles(assetsPath)
                    .Select(Path.GetFileName)
                    .Where(f => f != null && (f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".ogg")))
                    .Cast<string>();
                var picker = new ResourcePickerWindow("选择音频资源", resources);
                var result = await picker.ShowDialog<string?>(this);
                callback(result);
            };
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var dlg = new Window
        {
            Title = "错误",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = message, Margin = new Avalonia.Thickness(20,20,20,10), Foreground = Avalonia.Media.Brushes.Red },
                    new Button { Content = "确定", Width = 100, Margin = new Avalonia.Thickness(0,10,0,0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
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
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var textBox = new TextBox
        {
            Watermark = "请输入章节标题...",
            Margin = new Avalonia.Thickness(20, 20, 20, 10)
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
