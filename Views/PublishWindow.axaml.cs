using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MyBook.Services;

namespace MyBook.Views;

public partial class PublishWindow : Window
{
    private readonly PublishService _publishService;
    private bool _isPublishing;
    
    // 支持的目标平台
    private readonly List<(string DisplayName, string RuntimeId)> _runtimeOptions = new()
    {
        ("Windows x64 (推荐)", "win-x64"),
        ("Windows x86", "win-x86"),
        ("Windows ARM64", "win-arm64"),
        ("Linux x64", "linux-x64"),
        ("Linux ARM64", "linux-arm64"),
        ("macOS x64 (Intel)", "osx-x64"),
        ("macOS ARM64 (Apple Silicon)", "osx-arm64")
    };

    public PublishWindow()
    {
        InitializeComponent();
        
        // 创建 PublishService
        _publishService = new PublishService();
        _publishService.ProgressChanged += OnProgressChanged;

        // 初始化运行时选项
        InitializeRuntimeOptions();
        
        // 初始化时计算预估大小
        Loaded += async (s, e) => await UpdateEstimatedSizeAsync();
    }

    private void InitializeRuntimeOptions()
    {
        RuntimeComboBox.ItemsSource = _runtimeOptions.Select(r => r.DisplayName).ToList();
        
        // 默认选择当前平台
        var currentRid = GetCurrentRuntimeId();
        var defaultIndex = _runtimeOptions.FindIndex(r => r.RuntimeId == currentRid);
        RuntimeComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    private string GetCurrentRuntimeId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => "win-x64"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }
        return "win-x64";
    }

    private string GetSelectedRuntimeId()
    {
        var index = RuntimeComboBox.SelectedIndex;
        return index >= 0 && index < _runtimeOptions.Count 
            ? _runtimeOptions[index].RuntimeId 
            : "win-x64";
    }

    private async Task UpdateEstimatedSizeAsync()
    {
        try
        {
            // 自包含发布通常在 60-150MB 之间
            EstimatedSizeText.Text = "60-150 MB（取决于平台）";
        }
        catch
        {
            EstimatedSizeText.Text = "无法计算";
        }
        await Task.CompletedTask;
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择发布目录",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            var path = folder.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                // 创建以小说名称命名的子目录
                var novelName = string.IsNullOrWhiteSpace(NovelNameTextBox.Text) 
                    ? "MyNovel" 
                    : NovelNameTextBox.Text.Trim();
                    
                var targetPath = Path.Combine(path, CleanFileName(novelName));
                TargetPathTextBox.Text = targetPath;
            }
        }
    }

    private async void OnPublishClicked(object? sender, RoutedEventArgs e)
    {
        if (_isPublishing) return;

        // 验证输入
        var novelName = NovelNameTextBox.Text?.Trim();
        var targetPath = TargetPathTextBox.Text?.Trim();
        var runtimeId = GetSelectedRuntimeId();

        if (string.IsNullOrWhiteSpace(novelName))
        {
            await ShowMessageAsync("请输入小说名称", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            await ShowMessageAsync("请选择保存位置", "提示");
            return;
        }

        // 检查目标目录是否已存在
        if (Directory.Exists(targetPath) && Directory.GetFiles(targetPath).Length > 0)
        {
            var result = await ShowConfirmAsync(
                $"目录 \"{Path.GetFileName(targetPath)}\" 已存在且不为空。\n是否覆盖？",
                "确认覆盖");
            
            if (!result) return;
        }

        // 开始发布
        _isPublishing = true;
        SetUIEnabled(false);
        ProgressPanel.IsVisible = true;

        try
        {
            await _publishService.PublishAsync(targetPath, novelName, runtimeId);

            var exeName = runtimeId.StartsWith("win") ? $"{novelName}.exe" : novelName;
            await ShowMessageAsync(
                $"视觉小说已成功发布到:\n{targetPath}\n\n运行 {exeName} 即可开始游戏！",
                "发布成功 🎉");

            // 询问是否打开目录
            var openFolder = await ShowConfirmAsync("是否打开发布目录？", "发布完成");
            if (openFolder)
            {
                OpenFolder(targetPath);
            }

            Close();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"发布失败:\n{ex.Message}", "错误");
        }
        finally
        {
            _isPublishing = false;
            SetUIEnabled(true);
            ProgressPanel.IsVisible = false;
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (!_isPublishing)
        {
            Close();
        }
    }

    private void OnProgressChanged(string message, int percentage)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PublishProgressBar.Value = percentage;
            ProgressText.Text = message;
        });
    }

    private void SetUIEnabled(bool enabled)
    {
        NovelNameTextBox.IsEnabled = enabled;
        BrowseButton.IsEnabled = enabled;
        RuntimeComboBox.IsEnabled = enabled;
        PublishButton.IsEnabled = enabled;
        PublishButton.Content = enabled ? "开始发布" : "发布中...";
    }

    private async Task ShowMessageAsync(string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(25, 20)
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            Foreground = Avalonia.Media.Brushes.Black
        });

        var button = new Button
        {
            Content = "确定",
            Width = 100,
            Height = 36,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            Foreground = Avalonia.Media.Brushes.White
        };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private async Task<bool> ShowConfirmAsync(string message, string title)
    {
        var result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(25, 20)
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            Foreground = Avalonia.Media.Brushes.Black
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Spacing = 15
        };

        var cancelButton = new Button
        {
            Content = "取消",
            Width = 90,
            Height = 36,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#757575")),
            Foreground = Avalonia.Media.Brushes.White
        };
        cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

        var confirmButton = new Button
        {
            Content = "确定",
            Width = 90,
            Height = 36,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            Foreground = Avalonia.Media.Brushes.White
        };
        confirmButton.Click += (s, e) => { result = true; dialog.Close(); };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(this);

        return result;
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", path);
            }
            else if (OperatingSystem.IsLinux())
            {
                System.Diagnostics.Process.Start("xdg-open", path);
            }
        }
        catch
        {
            // 忽略打开文件夹的错误
        }
    }

    private string CleanFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}
