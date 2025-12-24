using System.IO;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBook.Models;
using MyBook.Services;
using Avalonia;
using Avalonia.Controls;

public partial class StoryEditorViewModel : ViewModelBase
{
    public Action<Action<string?>>? RequestImageSelect;
    public Action<Action<string?>>? RequestAudioSelect;

    [RelayCommand]
    private async Task SelectImageAsync()
    {
        if (SelectedNode == null) return;
        RequestImageSelect?.Invoke(result =>
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                SelectedNode.Visuals = new VisualData { BackgroundImage = Path.Combine("Assets", result) };
                _ = SaveNodeAsync();
            }
        });
    }

    [RelayCommand]
    private async Task SelectAudioAsync()
    {
        if (SelectedNode == null) return;
        RequestAudioSelect?.Invoke(result =>
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                SelectedNode.Audio = new AudioData { BgmFile = Path.Combine("Assets", result) };
                _ = SaveNodeAsync();
            }
        });
    }

    private readonly IStoryDataService _dataService;
    private readonly ScriptParser _parser;

    [ObservableProperty] private ObservableCollection<Chapter> _chapters = new();

    [ObservableProperty] private Chapter? _selectedChapter;

    [ObservableProperty] private ObservableCollection<StoryNodeExtended> _nodes = new();

    [ObservableProperty] private StoryNodeExtended? _selectedNode;

    [ObservableProperty] private string _importText = string.Empty;

    [ObservableProperty] private bool _isLoading;

    public StoryEditorViewModel(IStoryDataService dataService)
    {
        _dataService = dataService;
        _parser = new ScriptParser();
    }

  
    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await _dataService.InitializeDatabaseAsync();
            await LoadChaptersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

  
    private async Task LoadChaptersAsync()
    {
        var chapters = await _dataService.GetChaptersAsync();
        Chapters.Clear();
        foreach (var chapter in chapters)
        {
            Chapters.Add(chapter);
        }
    }

  
    partial void OnSelectedChapterChanged(Chapter? value)
    {
        if (value != null)
        {
            _ = LoadNodesAsync(value.Id);
        }
    }

    partial void OnSelectedNodeChanged(StoryNodeExtended? value)
    {
        // 节点变化时的处理逻辑
    }

   
 
    private async Task LoadNodesAsync(string chapterId, string? restoreNodeId = null)
    {
        var nodes = await _dataService.GetNodesAsync(chapterId);
        Nodes.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
        }

        // 恢复之前选中的节点
        if (!string.IsNullOrEmpty(restoreNodeId))
        {
            SelectedNode = Nodes.FirstOrDefault(n => n.Id == restoreNodeId);
        }

        System.Diagnostics.Debug.WriteLine($"[LoadNodes] ChapterId={chapterId}, NodeCount={nodes.Count()}, Restored={restoreNodeId}");
    }

    
    [RelayCommand]
    private async Task CreateChapterAsync(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = "新章节";

        var chapter = new Chapter
        {
            Id = $"chapter_{Chapters.Count + 1:D3}",
            Title = title,
            OrderIndex = Chapters.Count
        };
        try
        {
            await _dataService.SaveChapterAsync(chapter);
            System.Diagnostics.Debug.WriteLine($"[CreateChapterAsync] 章节已保存: {chapter.Id}");
            await LoadChaptersAsync(); // 保存后强制刷新章节列表
            SelectedChapter = chapter;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存章节失败: {ex.Message}");
            await ShowErrorDialog($"保存章节失败: {ex.Message}");
        }
    }

    
    [RelayCommand]
    private async Task DeleteChapterAsync()
    {
        if (SelectedChapter == null) return;

        var chapterToDelete = SelectedChapter;

        SelectedChapter = null;
        Nodes.Clear();

        await _dataService.DeleteChapterAsync(chapterToDelete.Id);
            await _dataService.DeleteChapterAsync(chapterToDelete.Id);
        Chapters.Remove(chapterToDelete);
    }

 
    [RelayCommand]
    private async Task CleanEmptyChaptersAsync()
    {
        var emptyChapters = new List<Chapter>();

        foreach (var chapter in Chapters)
        {
            var nodes = await _dataService.GetNodesAsync(chapter.Id);
            if (!nodes.Any())
            {
                emptyChapters.Add(chapter);
            }
        }

        System.Diagnostics.Debug.WriteLine($"[CleanEmptyChapters] Found {emptyChapters.Count} empty chapters");

        if (emptyChapters.Any())
        {
            if (SelectedChapter != null && emptyChapters.Contains(SelectedChapter))
            {
                SelectedChapter = null;
                Nodes.Clear();
            }

            foreach (var chapter in emptyChapters)
            {
                await _dataService.DeleteChapterAsync(chapter.Id);
                Chapters.Remove(chapter);
                System.Diagnostics.Debug.WriteLine($"[CleanEmptyChapters] Deleted: {chapter.Id} - {chapter.Title}");
            }
        }
    }

 
    [RelayCommand]
    private async Task CreateNodeAsync()
    {
        if (SelectedChapter == null) return;

        var node = new StoryNodeExtended
        {
            Id = $"{SelectedChapter.Id}_node_{Nodes.Count:D4}",
            ChapterId = SelectedChapter.Id,
            Type = NodeType.Narration,
            Text = "新节点内容",
            OrderIndex = Nodes.Count
        };
        await _dataService.SaveNodeAsync(node, true);
        System.Diagnostics.Debug.WriteLine($"[CreateNodeAsync] 节点已保存: {node.Id}");
        Nodes.Add(node);
        SelectedNode = node;
    }

    /// <summary>
    /// 为当前选中节点添加一个默认选项
    /// </summary>
    [RelayCommand]
    private async Task AddChoiceAsync()
    {
        if (SelectedNode == null) return;
        var choice = new MyBook.Models.StoryChoice { Text = "新选项", TargetNodeId = string.Empty, TargetChapterId = null, Condition = null };
        SelectedNode.Choices.Add(choice);
        var currentNodeId = SelectedNode.Id;
        await _dataService.SaveNodeAsync(SelectedNode, true);
        System.Diagnostics.Debug.WriteLine($"[AddChoiceAsync] Added choice to {SelectedNode.Id}");
        await LoadNodesAsync(SelectedNode.ChapterId, currentNodeId);
    }

    /// <summary>
    /// 删除当前选中节点的指定选项
    /// </summary>
    [RelayCommand]
    private async Task RemoveChoiceAsync(MyBook.Models.StoryChoice? choice)
    {
        if (SelectedNode == null || choice == null) return;
        SelectedNode.Choices.Remove(choice);
        var currentNodeId = SelectedNode.Id;
        await _dataService.SaveNodeAsync(SelectedNode, true);
        System.Diagnostics.Debug.WriteLine($"[RemoveChoiceAsync] Removed choice from {SelectedNode.Id}");
        await LoadNodesAsync(SelectedNode.ChapterId, currentNodeId);
    }

    /// <summary>
    /// 将当前节点标记为结局或取消结局
    /// </summary>
    [RelayCommand]
    private async Task ToggleEndingAsync()
    {
        if (SelectedNode == null) return;
        var currentNodeId = SelectedNode.Id;
        SelectedNode.Type = SelectedNode.Type == MyBook.Models.NodeType.Ending ? MyBook.Models.NodeType.Dialogue : MyBook.Models.NodeType.Ending;
        await _dataService.SaveNodeAsync(SelectedNode, true);
        System.Diagnostics.Debug.WriteLine($"[ToggleEndingAsync] Node {SelectedNode.Id} type set to {SelectedNode.Type}");
        await LoadNodesAsync(SelectedNode.ChapterId, currentNodeId);
    }

   
    [RelayCommand]
    private async Task SaveNodeAsync()
    {
        if (SelectedNode == null) return;
        try
        {
            // 在保存前规范化本节点的选项目标（防止把 node id 填到 TargetChapterId）
            await NormalizeNodeChoicesAsync(SelectedNode);

            var currentNodeId = SelectedNode.Id;
            await _dataService.SaveNodeAsync(SelectedNode, true);
            if (SelectedChapter != null)
            {
                await LoadNodesAsync(SelectedChapter.Id, currentNodeId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存节点失败: {ex.Message}");
            await ShowErrorDialog($"保存节点失败: {ex.Message}");
        }
    }

    
    private async Task ShowErrorDialog(string message)
    {
        var dlg = new Avalonia.Controls.Window
        {
            Title = "错误",
            Width = 400,
            Height = 150,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            CanResize = false,
            Content = new Avalonia.Controls.StackPanel
            {
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = message, Margin = new Avalonia.Thickness(20, 20, 20, 10),
                        Foreground = Avalonia.Media.Brushes.Red
                    },
                    new Avalonia.Controls.Button
                    {
                        Content = "确定", Width = 100, Margin = new Avalonia.Thickness(0, 10, 0, 0),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            }
        };
        ((Avalonia.Controls.Button)((Avalonia.Controls.StackPanel)dlg.Content).Children[1]).Click +=
            (s, e) => dlg.Close();
        dlg.Show();
    }

    private async Task ShowInfoDialog(string message)
    {
        var dlg = new Avalonia.Controls.Window
        {
            Title = "提示",
            Width = 420,
            Height = 160,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            CanResize = false,
            Content = new Avalonia.Controls.StackPanel
            {
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = message, Margin = new Avalonia.Thickness(20, 20, 20, 10),
                        Foreground = Avalonia.Media.Brushes.Black
                    },
                    new Avalonia.Controls.Button
                    {
                        Content = "确定", Width = 100, Margin = new Avalonia.Thickness(0, 10, 0, 0),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            }
        };
        ((Avalonia.Controls.Button)((Avalonia.Controls.StackPanel)dlg.Content).Children[1]).Click +=
            (s, e) => dlg.Close();
        dlg.Show();
    }

    /// <summary>
    /// 规范化一个节点中所有选项的目标字段：如果作者误把 node id 填到 TargetChapterId，则把它移到 TargetNodeId，并设置 TargetChapterId 为对应章节 id（或根据命名规则推断）
    /// </summary>
    private async Task NormalizeNodeChoicesAsync(StoryNodeExtended node)
    {
        if (node?.Choices == null) return;
        int corrected = 0;
        foreach (var choice in node.Choices)
        {
            if (choice == null) continue;
           
            if (string.IsNullOrWhiteSpace(choice.TargetNodeId) && !string.IsNullOrWhiteSpace(choice.TargetChapterId))
            {
                var possibleNode = await _dataService.GetNodeAsync(choice.TargetChapterId!);
                if (possibleNode != null)
                {
                    choice.TargetNodeId = possibleNode.Id;
                    choice.TargetChapterId = possibleNode.ChapterId;
                    corrected++;
                    System.Diagnostics.Debug.WriteLine($"[Normalize] Moved mistaken node-id from TargetChapterId to TargetNodeId: {choice.TargetNodeId}");
                    continue;
                }

               
                if (choice.TargetChapterId!.Contains("_node_"))
                {
                    var parts = choice.TargetChapterId.Split(new[] {"_node_"}, StringSplitOptions.None);
                    if (parts.Length >= 1)
                    {
                        var derivedChapterId = parts[0];
                        choice.TargetNodeId = choice.TargetChapterId;
                        choice.TargetChapterId = derivedChapterId;
                        corrected++;
                        System.Diagnostics.Debug.WriteLine($"[Normalize] Guessed chapter id '{derivedChapterId}' from mistaken node id and moved node id to TargetNodeId.");
                        continue;
                    }
                }
            }
        }

        if (corrected > 0)
        {
            await ShowInfoDialog($"已自动修正 {corrected} 个选项的目标：把误填的节点 id 移入 TargetNodeId。建议检查并保存。");
        }
    }

   
    [RelayCommand]
    private async Task DeleteNodeAsync()
    {
        if (SelectedNode == null) return;

        var nodeToDelete = SelectedNode;

        SelectedNode = null;

        await _dataService.DeleteNodeAsync(nodeToDelete.Id);
        Nodes.Remove(nodeToDelete);
    }

  
    [RelayCommand]
    private async Task ImportScriptAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportText)) return;

        IsLoading = true;
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ImportScript] Starting parse, text length={ImportText.Length}");

            var (chapter, nodes) = await _parser.ParseScriptAsync(ImportText, $"chapter_{Chapters.Count + 1:D3}");

            System.Diagnostics.Debug.WriteLine(
                $"[ImportScript] Parsed: Chapter={chapter?.Title ?? "null"}, NodeCount={nodes?.Count ?? 0}");

            chapter.OrderIndex = Chapters.Count;

            await _dataService.SaveChapterAsync(chapter);
            Chapters.Add(chapter);

            System.Diagnostics.Debug.WriteLine($"[ImportScript] Chapter saved: Id={chapter.Id}, Title={chapter.Title}");

            foreach (var node in nodes)
            {
                await _dataService.SaveNodeAsync(node);
                System.Diagnostics.Debug.WriteLine(
                    $"[ImportScript] Node saved: Id={node.Id}, Text length={node.Text.Length}");
            }

            SelectedChapter = chapter;

            System.Diagnostics.Debug.WriteLine($"[ImportScript] Import completed! Total chapters: {Chapters.Count}");

            ImportText = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportScript] ERROR: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    
    [RelayCommand]
    private async Task<string> ExportScriptAsync()
    {
        if (SelectedChapter == null) return string.Empty;
        return await _dataService.ExportToTextAsync(SelectedChapter.Id);
    }

    /// <summary>
    /// 保存当前章节
    /// </summary>
    [RelayCommand]
    public async Task SaveChapterAsync()
    {
        if (SelectedChapter == null)
        {
            System.Diagnostics.Debug.WriteLine("SaveChapterAsync: SelectedChapter is null");
            await ShowErrorDialog("保存章节失败: 当前未选中章节。");
            return;
        }

        try
        {
            if (SelectedChapter == null)
            {
                await ShowErrorDialog("保存章节失败：当前未选中章节。");
                return;
            }
            if (Nodes == null)
            {
                await ShowErrorDialog("保存章节失败：节点集合为 null");
                return;
            }
            if (_dataService == null)
            {
                await ShowErrorDialog("保存章节失败：数据服务未初始化");
                return;
            }
            int savedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;
            foreach (var node in Nodes)
            {
                if (node == null)
                {
                    skippedCount++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Text))
                {
                    skippedCount++;
                    continue;
                }
                try
                {
                    await NormalizeNodeChoicesAsync(node);
                    await _dataService.SaveNodeAsync(node);
                    savedCount++;
                    System.Diagnostics.Debug.WriteLine($"[SaveChapterAsync] 节点已保存: {node.Id}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await ShowErrorDialog($"保存节点失败：{ex.Message}\n节点ID: {node.Id ?? "(null)"}");
                }
            }
            try
            {
                await _dataService.SaveChapterAsync(SelectedChapter);
                System.Diagnostics.Debug.WriteLine($"[SaveChapterAsync] 章节已保存: {SelectedChapter.Id}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"保存章节对象失败：{ex.Message}");
                return;
            }
            await LoadChaptersAsync();
            if (SelectedChapter != null && !string.IsNullOrWhiteSpace(SelectedChapter.Id))
            {
                var currentNodeId = SelectedNode?.Id;
                await LoadNodesAsync(SelectedChapter.Id, currentNodeId);
            }
            System.Diagnostics.Debug.WriteLine($"[SaveChapterAsync] 保存完成，成功节点: {savedCount}，跳过: {skippedCount}，错误: {errorCount}");
            
        }
        finally
        {
        }
    }
}
            
