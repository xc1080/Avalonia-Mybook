namespace MyBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBook.Models;
using MyBook.Services;
using NCalc;

/// <summary>
/// 正在使用的版本        
/// </summary>
public partial class StoryViewModelRuntime : ViewModelBase
{
    private readonly MyBook.Services.AudioPlayer _audioPlayer = new();
    private string? _currentBgmPath;
    [ObservableProperty]
    private string? _backgroundImage;
    private void PlayMusic(string bgmPath)
    {
        if (string.IsNullOrWhiteSpace(bgmPath)) return;
        if (bgmPath == _currentBgmPath) return;
        try
        {
            _audioPlayer.Play(bgmPath);
            _currentBgmPath = bgmPath;
            
        }
        catch { }
    }

    private void StopMusic() 
    {
        try
        {
            _audioPlayer.Stop();
        }
        catch { }
        _currentBgmPath = null;
    }
    [ObservableProperty]
    private StoryNodeExtended? _currentNode;

    private readonly IStoryDataService _dataService;
    private Dictionary<string, StoryNodeExtended> _nodeCache = new();
    private readonly Stack<string> _historyStack;
    private string? _currentChapterId;
    [ObservableProperty]
    private System.Collections.Generic.List<MyBook.Models.Chapter> _availableChapters = new();

    // 记录玩家已做出的选择（以 StoryChoice.Id 标识）
    private readonly System.Collections.Generic.HashSet<string> _chosenChoiceIds = new();

    public System.Collections.Generic.IReadOnlyCollection<string> ChosenChoices => _chosenChoiceIds;

    public StoryViewModelRuntime(IStoryDataService dataService)
    {
        _dataService = dataService;
        _historyStack = new Stack<string>();
    }

    /// <summary>
    /// Refresh the available chapters from the data service (UI can call this to get latest list)
    /// </summary>
    public async Task RefreshAvailableChaptersAsync()
    {
        try
        {
            var chapters = await _dataService.GetChaptersAsync();
            AvailableChapters = chapters;
            OnPropertyChanged(nameof(AvailableChapters));
            OnPropertyChanged(nameof(CanGoToNextChapter));
        }
        catch { }
    }

    /// <summary>
    /// 初始化并加载故事
    /// </summary>
    public async Task InitializeAsync()
    {
        await _dataService.InitializeDatabaseAsync();
        var chapters = await _dataService.GetChaptersAsync();
        AvailableChapters = chapters;
        if (chapters.Any())
        {
            await LoadChapterAsync(chapters.First().Id);
        }
    }

    /// <summary>
    /// 加载指定章节
    /// </summary>
    public async Task LoadChapterAsync(string chapterId)
    {
        // Refresh available chapters so runtime has up-to-date ordering and ids
        try
        {
            var chapters = await _dataService.GetChaptersAsync();
            AvailableChapters = chapters;
            OnPropertyChanged(nameof(AvailableChapters));
        }
        catch { }

        var nodes = await _dataService.GetNodesAsync(chapterId);

        _nodeCache.Clear();
        foreach (var node in nodes)
        {
            _nodeCache[node.Id] = node;
        }

        _currentChapterId = chapterId;
        CurrentNode = nodes.FirstOrDefault();
        OnPropertyChanged(nameof(CanGoToNextChapter));
    }

    /// <summary>
    /// 当节点改变时通知相关属性更新
    /// </summary>
    partial void OnCurrentNodeChanged(StoryNodeExtended? value)
    {
        
        var newBgm = value?.Audio?.BgmFile;
        if (!string.IsNullOrWhiteSpace(newBgm))
        {
            if (newBgm != _currentBgmPath)
            {
                PlayMusic(newBgm);
                _currentBgmPath = newBgm;
            }
        }

        
        var newBg = value?.Visuals?.BackgroundImage;
        if (!string.IsNullOrWhiteSpace(newBg))
        {
            if (newBg != BackgroundImage)
            {
                BackgroundImage = newBg;
            }
        }
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(CanPrev));
        OnPropertyChanged(nameof(HasChoices));
        OnPropertyChanged(nameof(VisibleChoices));
        OnPropertyChanged(nameof(HasVisibleChoices));
        OnPropertyChanged(nameof(CanGoToNextChapter));
        OnPropertyChanged(nameof(CanRestart));
        
        try
        {
            NextCommand.NotifyCanExecuteChanged();
        }
        catch { }
        try
        {
            PrevCommand.NotifyCanExecuteChanged();
        }
        catch { }
        try
        {
            RestartCommand.NotifyCanExecuteChanged();
        }
        catch { }
    }

    /// <summary>
    /// 检查是否可以前进
    /// </summary>
    public bool CanNext => CurrentNode?.NextId != null && !HasVisibleChoices;

    /// <summary>
    /// 检查是否可以返回
    /// </summary>
    public bool CanPrev => CurrentNode?.PrevId != null && !HasVisibleChoices;

    /// <summary>
    /// 检查是否有选项
    /// </summary>
    public bool HasChoices => CurrentNode?.Choices.Count > 0;

    // Expose choices but filter out '继续阅读' when the runtime will show the Next Chapter flow
    public System.Collections.Generic.IEnumerable<MyBook.Models.StoryChoice> VisibleChoices
    {
        get
        {
            if (CurrentNode?.Choices == null) return System.Linq.Enumerable.Empty<MyBook.Models.StoryChoice>();
            return CurrentNode.Choices.Where(c => {
                var t = c.Text ?? string.Empty;
                // always hide '继续' continuation style options
                if (t.IndexOf("继续", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                // hide one-time choices that have already been chosen
                if (c.IsOneTime && !string.IsNullOrWhiteSpace(c.Id) && _chosenChoiceIds.Contains(c.Id)) return false;
                // otherwise show
                return true;
            });
        }
    }

    public bool HasVisibleChoices => VisibleChoices.Any();

    /// <summary>
    /// 是否允许重新开始（当存在可见选项时禁止重新开始）
    /// </summary>
    public bool CanRestart => !HasVisibleChoices;

    /// <summary>
    /// 是否可以前往下一章（当当前节点是结局且后续章节存在时）
    /// </summary>
    public bool CanGoToNextChapter
    {
        get
        {
            // Consider chapter end when current node is explicitly an Ending
            // or when it has no NextId and no *visible* choices (we treat continuation-only choices as invisible)
            if (CurrentNode == null) return false;
            bool hasVisibleChoices = false;
            if (CurrentNode.Choices != null)
            {
                hasVisibleChoices = CurrentNode.Choices.Any(c => {
                    var t = c.Text ?? string.Empty;
                    return t.IndexOf("继续", StringComparison.OrdinalIgnoreCase) < 0;
                });
            }
            // 如果节点标记为结局，则只有在没有 EndingCondition 或满足 EndingCondition 时才视为结局
            bool endsByCondition = true;
            if (CurrentNode.IsEnding && !string.IsNullOrWhiteSpace(CurrentNode.EndingCondition))
            {
                endsByCondition = EvaluateCondition(CurrentNode.EndingCondition);
            }
            var isChapterEnd = (CurrentNode.IsEnding && endsByCondition) || (string.IsNullOrWhiteSpace(CurrentNode.NextId) && !hasVisibleChoices);
            if (!isChapterEnd) return false;
            if (string.IsNullOrWhiteSpace(_currentChapterId)) return false;
            if (AvailableChapters == null || AvailableChapters.Count == 0) return false;
            var idx = AvailableChapters.FindIndex(c => c.Id == _currentChapterId);
            return idx >= 0 && idx < AvailableChapters.Count - 1;
        }
    }

                
    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        if (HasVisibleChoices) return; // 禁止在存在可见选项时使用下一页按钮
        if (CurrentNode?.NextId != null)
        {
            await NavigateToNodeAsync(CurrentNode.NextId);
        }
    }

    /// <summary>
    /// 当前章节结局后前往下一章节
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
    private async Task GoToNextChapterAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentChapterId)) return;

        // Refresh chapter list to ensure runtime has the latest chapters (editor may add chapters at runtime)
        try
        {
            var chapters = await _dataService.GetChaptersAsync();
            AvailableChapters = chapters;
        }
        catch { }

        if (AvailableChapters == null || AvailableChapters.Count == 0) return;
        var idx = AvailableChapters.FindIndex(c => c.Id == _currentChapterId);
        if (idx >= 0 && idx < AvailableChapters.Count - 1)
        {
            var next = AvailableChapters[idx + 1];
            await LoadChapterAsync(next.Id);
        }
    }

    /// <summary>
    /// 选择并加载指定章节
    /// </summary>
    [RelayCommand]
    private async Task SelectChapterAsync(string? chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId)) return;
        await LoadChapterAsync(chapterId);
    }

    /// <summary>
    /// 返回上一页
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPrev))]
    private async Task PrevAsync()
    {
        if (HasVisibleChoices) return; // 禁止在存在可见选项时使用上一页
        if (CurrentNode?.PrevId != null)
        {
            await NavigateToNodeAsync(CurrentNode.PrevId);
        }
    }

    /// <summary>
    /// 根据选择导航
    /// </summary>
    [RelayCommand]
    private async Task NavigateAsync(string? targetNodeId)
    {
        if (targetNodeId != null)
        {
            await NavigateToNodeAsync(targetNodeId);
        }
    }

    /// <summary>
    /// 根据一个选择（Choice）执行导航：可在同章内跳转节点，或跳转到另一个章节（决策树分支）
    /// </summary>
    [RelayCommand]
    private async Task ChooseAsync(MyBook.Models.StoryChoice? choice)
    {
        if (choice == null)
        {
            FileLogger.Log("ChooseAsync called with null choice");
            return;
        }

        FileLogger.Log($"ChooseAsync invoked: Id={choice.Id}, TargetChapterId={choice.TargetChapterId}, TargetNodeId={choice.TargetNodeId}");

        // 记录玩家已选择的选项（以 choice.Id 为标识）
        try
        {
            if (!string.IsNullOrWhiteSpace(choice.Id)) _chosenChoiceIds.Add(choice.Id);
        }
        catch (Exception ex)
        {
            FileLogger.Log($"Error recording chosen id: {ex}");
        }

        // 优先使用 TargetNodeId（若填写），否则尝试按 TargetChapterId 处理。
        if (!string.IsNullOrWhiteSpace(choice.TargetNodeId))
        {
            await NavigateToNodeAsync(choice.TargetNodeId);
            try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = choice.TargetNodeId }); } catch { }
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice.TargetChapterId))
        {
            // 如果 TargetChapterId 实际上是一个章节 id，则加载章节；否则把它当作 node id 的备用跳转（编辑器可能把 node id 填入了错误的字段）
            try
            {
                var possibleChapter = await _dataService.GetChapterAsync(choice.TargetChapterId);
                if (possibleChapter != null)
                {
                    await LoadChapterAsync(choice.TargetChapterId);
                    // persist game state after chapter jump
                    try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = choice.TargetChapterId }); } catch { }
                    return;
                }
            }
            catch { }

            // 退化处理：把 TargetChapterId 当作 node id 进行跳转尝试
            await NavigateToNodeAsync(choice.TargetChapterId);
            try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = choice.TargetChapterId }); } catch { }
            return;
        }
        // if neither target specified, still persist current chosen set
        try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id }); } catch { }
    }

    /// <summary>
    /// 导航到指定节点
    /// </summary>
    private async Task NavigateToNodeAsync(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            FileLogger.Log("NavigateToNodeAsync called with empty nodeId");
            return;
        }

        var trimmed = nodeId.Trim();
        FileLogger.Log($"NavigateToNodeAsync: attempting to navigate to '{trimmed}'");

        if (_nodeCache.TryGetValue(trimmed, out var node))
        {
            if (CurrentNode != null)
            {
                _historyStack.Push(CurrentNode.Id);
            }
            _currentChapterId = node.ChapterId;
            CurrentNode = node;
            FileLogger.Log($"NavigateToNodeAsync: navigated using cache to '{trimmed}' (chapter {_currentChapterId})");
            return;
        }

        node = await _dataService.GetNodeAsync(trimmed);
        if (node != null)
        {
            _nodeCache[trimmed] = node;
            if (CurrentNode != null)
            {
                _historyStack.Push(CurrentNode.Id);
            }
            _currentChapterId = node.ChapterId;
            CurrentNode = node;
            FileLogger.Log($"NavigateToNodeAsync: navigated after DB lookup to '{trimmed}' (chapter {_currentChapterId})");
            return;
        }

        // Fallback: try searching all available chapters for a matching node id (exact or suffix match)
        try
        {
            if (AvailableChapters != null)
            {
                foreach (var ch in AvailableChapters)
                {
                    var nodes = await _dataService.GetNodesAsync(ch.Id);
                    var found = nodes.FirstOrDefault(n => string.Equals(n.Id, trimmed, StringComparison.OrdinalIgnoreCase)
                        || n.Id.EndsWith(trimmed, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        _nodeCache[found.Id] = found;
                        if (CurrentNode != null)
                        {
                            _historyStack.Push(CurrentNode.Id);
                        }
                        _currentChapterId = found.ChapterId;
                        CurrentNode = found;
                        FileLogger.Log($"NavigateToNodeAsync: navigated after searching chapters to '{found.Id}' (chapter {_currentChapterId})");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"NavigateToNodeAsync: error during fallback search: {ex}");
        }

        FileLogger.Log($"NavigateToNodeAsync: node '{trimmed}' not found in cache, DB, or chapters");
    }


    private bool EvaluateCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        var c = condition.Trim();
        if (c.StartsWith("chose:", StringComparison.OrdinalIgnoreCase))
        {
            var id = c.Substring(6).Trim();
            return !string.IsNullOrWhiteSpace(id) && _chosenChoiceIds.Contains(id);
        }
        if (c.StartsWith("notchose:", StringComparison.OrdinalIgnoreCase))
        {
            var id = c.Substring(9).Trim();
            return string.IsNullOrWhiteSpace(id) || !_chosenChoiceIds.Contains(id);
        }
    
        try
        {
            var e = new Expression(c);
            e.EvaluateFunction += (name, args) =>
            {
                if (string.Equals(name, "chose", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Parameters.Length > 0)
                    {
                        var p = args.Parameters[0];
                        var sid = p == null ? string.Empty : p.ToString() ?? string.Empty;
                   
                        if (sid.Length >= 2 && ((sid.StartsWith("\"") && sid.EndsWith("\"")) || (sid.StartsWith("'") && sid.EndsWith("'"))))
                        {
                            sid = sid.Substring(1, sid.Length - 2);
                        }
                        args.Result = !string.IsNullOrWhiteSpace(sid) && _chosenChoiceIds.Contains(sid);
                    }
                    else
                    {
                        args.Result = false;
                    }
                }
            };
            var result = e.Evaluate();
            if (result is bool b) return b;
            if (result is int i) return i != 0;
            return false;
        }
        catch
        {
            return false;
        }
    }

 
    [RelayCommand(CanExecute = nameof(CanRestart))]
    private async Task RestartAsync()
    {
        _historyStack.Clear();
        
        var chapters = await _dataService.GetChaptersAsync();
        if (chapters.Any())
        {
            await LoadChapterAsync(chapters.First().Id);
        }
    }
}
