using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using MyBook.ViewModels;
using MyBook.Models;
using System.Threading.Tasks;
using Avalonia.Data;
using System.Linq;

namespace MyBook.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is StoryViewModelRuntime vm)
        {
            vm.ReturnToLauncherRequested += OnReturnToLauncher;
        }
    }
    
    private void OnReturnToLauncher()
    {
        // 打开启动器窗口
        var launcher = new LauncherWindow();
        launcher.Show();
        // 关闭当前窗口
        Close();
    }

    /// <summary>
    /// 点击文本区域时跳过打字机效果
    /// </summary>
    private void OnTextAreaClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is StoryViewModelRuntime vm)
        {
            if (vm.IsTyping)
            {
                vm.SkipTypewriterCommand.Execute(null);
            }
        }
    }

    private async void OnManageSavesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StoryViewModelRuntime vm) return;
        var dlg = new SaveLoadWindow
        {
            DataContext = vm
        };
        await dlg.ShowDialog(this);
    }

    private async void OnNextChapterClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StoryViewModelRuntime vm) return;
        // Refresh chapters via VM helper to ensure latest
        await vm.RefreshAvailableChaptersAsync();

        var list = vm.AvailableChapters;
        if (list == null || list.Count == 0) return;

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
            ItemsSource = list,
            DisplayMemberBinding = new Binding("Title"),
            Height = 180,
            Background = Avalonia.Media.Brushes.White,
            Foreground = Avalonia.Media.Brushes.Black
        };

        var okButton = new Button
        {
            Content = "开始章节",
            Width = 120,
            Height = 38,
            Margin = new Avalonia.Thickness(0, 15, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            IsEnabled = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            Foreground = Avalonia.Media.Brushes.White
        };

        listBox.SelectionChanged += (s, ev) => okButton.IsEnabled = listBox.SelectedItem != null;
        okButton.Click += (s, ev) => dialog.Close(listBox.SelectedItem);
        panel.Children.Add(listBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        var selected = await dialog.ShowDialog<object>(this);
        if (selected is Chapter chapter)
        {
            await vm.LoadChapterAsync(chapter.Id);
        }
    }
}