using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MyBook.Views;

public partial class ResourceBrowserWindow : Window
{
    public string? SelectedResource { get; private set; }
    public string? SelectedFullPath { get; private set; }

    private readonly string _assetsRoot;
    private readonly string[] _fileFilters;
    private readonly string _resourceType;
    private readonly ObservableCollection<ResourceItem> _resources = new();

    /// <summary>
    /// 默认构造函数（用于 XAML 设计器）
    /// </summary>
    public ResourceBrowserWindow() : this("选择资源", "资源", new[] { ".png", ".jpg" })
    {
    }

    /// <summary>
    /// 创建资源浏览器窗口
    /// </summary>
    /// <param name="title">窗口标题</param>
    /// <param name="resourceType">资源类型描述，例如 "图片" 或 "音频"</param>
    /// <param name="fileExtensions">允许的文件扩展名，例如 [".png", ".jpg"]</param>
    public ResourceBrowserWindow(string title, string resourceType, string[] fileExtensions)
    {
        InitializeComponent();

        Title = title;
        _resourceType = resourceType;
        _fileFilters = fileExtensions;

        // 默认资源目录
        var assetsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets");
        _assetsRoot = Path.GetFullPath(assetsPath);

        var resourceList = this.FindControl<ListBox>("ResourceList");
        var browseFolder = this.FindControl<Button>("BrowseFolderBtn");
        var browseFile = this.FindControl<Button>("BrowseFileBtn");
        var cancelBtn = this.FindControl<Button>("CancelBtn");
        var confirmBtn = this.FindControl<Button>("ConfirmBtn");
        var listTitle = this.FindControl<TextBlock>("ListTitleText");

        if (listTitle != null) listTitle.Text = $"可用{resourceType}";

        if (resourceList != null)
        {
            resourceList.ItemsSource = _resources;
            resourceList.SelectionChanged += OnResourceSelectionChanged;
            resourceList.DoubleTapped += OnResourceDoubleTapped;
        }

        if (browseFolder != null) browseFolder.Click += OnBrowseFolderClick;
        if (browseFile != null) browseFile.Click += OnBrowseFileClick;
        if (cancelBtn != null) cancelBtn.Click += (_, _) => Close(null);
        if (confirmBtn != null) confirmBtn.Click += OnConfirmClick;

        Loaded += async (_, _) => await ScanResourcesAsync(_assetsRoot);
    }

    private async Task ScanResourcesAsync(string folderPath)
    {
        _resources.Clear();
        UpdatePathText(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        await Task.Run(() =>
        {
            // 扫描当前目录的文件
            var files = Directory.GetFiles(folderPath)
                .Where(f => IsMatchingFile(f))
                .ToList();

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(_assetsRoot, file);
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _resources.Add(new ResourceItem
                    {
                        DisplayName = fileName,
                        RelativePath = relativePath,
                        FullPath = file,
                        IsFolder = false,
                        FileType = GetFileType(file)
                    });
                });
            }

            // 扫描子目录
            var dirs = Directory.GetDirectories(folderPath);
            foreach (var dir in dirs)
            {
                var subFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(f => IsMatchingFile(f))
                    .ToList();

                foreach (var file in subFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var relativePath = Path.GetRelativePath(_assetsRoot, file);
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _resources.Add(new ResourceItem
                        {
                            DisplayName = fileName,
                            RelativePath = relativePath,
                            FullPath = file,
                            IsFolder = false,
                            FileType = GetFileType(file)
                        });
                    });
                }
            }
        });
    }
    
    private static string GetFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" => "Image",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" => "Audio",
            _ => "Other"
        };
    }

    private void OnResourceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var selected = listBox?.SelectedItem as ResourceItem;

        if (selected == null)
        {
            UpdateSelectedPathText("(无)");
            var confirmBtn = this.FindControl<Button>("ConfirmBtn");
            if (confirmBtn != null) confirmBtn.IsEnabled = false;
            return;
        }

        SelectedResource = selected.RelativePath;
        SelectedFullPath = selected.FullPath;
        UpdateSelectedPathText(selected.DisplayName);

        var confirm = this.FindControl<Button>("ConfirmBtn");
        if (confirm != null) confirm.IsEnabled = true;
    }

    private void OnResourceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedResource != null)
        {
            Close(new ResourceBrowserResult
            {
                RelativePath = SelectedResource,
                FullPath = SelectedFullPath
            });
        }
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        if (storage == null) return;

        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"选择{_resourceType}文件夹",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var folder = result[0];
            var path = folder.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                await ScanResourcesAsync(path);
            }
        }
    }

    private async void OnBrowseFileClick(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        if (storage == null) return;

        var patterns = _fileFilters.Select(ext => "*" + ext).ToArray();
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"选择{_resourceType}文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType($"{_resourceType}文件") { Patterns = patterns }
            }
        });

        if (result.Count > 0)
        {
            var file = result[0];
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                // 检查是否在 Assets 目录下
                if (path.StartsWith(_assetsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedResource = Path.GetRelativePath(_assetsRoot, path);
                }
                else
                {
                    SelectedResource = path;
                }
                SelectedFullPath = path;

                UpdateSelectedPathText(Path.GetFileName(path));
                var confirmBtn = this.FindControl<Button>("ConfirmBtn");
                if (confirmBtn != null) confirmBtn.IsEnabled = true;

                // 直接确认选择
                Close(new ResourceBrowserResult
                {
                    RelativePath = SelectedResource,
                    FullPath = SelectedFullPath
                });
            }
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(new ResourceBrowserResult
        {
            RelativePath = SelectedResource,
            FullPath = SelectedFullPath
        });
    }

    private void UpdatePathText(string path)
    {
        var text = this.FindControl<TextBlock>("CurrentPathText");
        if (text != null) text.Text = path;
    }

    private void UpdateSelectedPathText(string path)
    {
        var text = this.FindControl<TextBlock>("SelectedPathText");
        if (text != null) text.Text = path;
    }

    private bool IsMatchingFile(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return _fileFilters.Contains(ext);
    }
}

public class ResourceItem
{
    public string DisplayName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsFolder { get; set; }
    
    /// <summary>
    /// 文件类型：Image, Audio, Other
    /// </summary>
    public string FileType { get; set; } = "Other";
    
    /// <summary>
    /// 文件类型显示名称
    /// </summary>
    public string FileTypeLabel => FileType switch
    {
        "Image" => "图片",
        "Audio" => "音频",
        _ => "文件"
    };
    
    /// <summary>
    /// 文件类型对应的背景颜色
    /// </summary>
    public string FileTypeBackground => FileType switch
    {
        "Image" => "#E3F2FD",    // 浅蓝色
        "Audio" => "#FFF3E0",    // 浅橙色
        _ => "#E8F5E9"           // 浅绿色
    };
    
    /// <summary>
    /// 文件类型对应的前景颜色
    /// </summary>
    public string FileTypeForeground => FileType switch
    {
        "Image" => "#1565C0",    // 深蓝色
        "Audio" => "#E65100",    // 深橙色
        _ => "#2E7D32"           // 深绿色
    };
}

public class ResourceBrowserResult
{
    public string? RelativePath { get; set; }
    public string? FullPath { get; set; }
}
