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
    private readonly TypewriterService _typewriter = new();
    private string? _currentBgmPath;
    
    // 用于抑制读档时的打字机效果
    private bool _suppressTypewriter;
    
    [ObservableProperty]
    private string? _backgroundImage;
    
    // 打字机效果：当前显示的文本
    [ObservableProperty]
    private string _displayedText = string.Empty;
    
    // 打字机效果：是否正在打字
    [ObservableProperty]
    private bool _isTyping;
    
    // 打字机效果配置
    public bool EnableTypewriter { get; set; } = true;
    public int TypewriterSpeed { get => _typewriter.CharacterDelay; set => _typewriter.CharacterDelay = value; }
    
    [ObservableProperty]
    private bool _enableTypeSound = true;
    
    partial void OnEnableTypeSoundChanged(bool value)
    {
        _typewriter.EnableSound = value;
    }
    
    // 背景音乐开关
    [ObservableProperty]
    private bool _enableBgm = true;
    
    partial void OnEnableBgmChanged(bool value)
    {
        if (!value)
        {
            StopMusic();
        }
        else if (CurrentNode?.Audio?.BgmFile != null)
        {
            PlayMusic(CurrentNode.Audio.BgmFile);
        }
    }
    
    // 返回主菜单事件
    public event Action? ReturnToLauncherRequested;
    
    [RelayCommand]
    private void ReturnToLauncher()
    {
        StopMusic();
        ReturnToLauncherRequested?.Invoke();
    }
    
    private void PlayMusic(string bgmPath)
    {
        if (string.IsNullOrWhiteSpace(bgmPath)) return;
        if (!EnableBgm) return;
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

    // 记录玩家已做出的选择
    private readonly System.Collections.Generic.HashSet<string> _chosenChoiceIds = new();

    public System.Collections.Generic.IReadOnlyCollection<string> ChosenChoices => _chosenChoiceIds;

    public StoryViewModelRuntime(IStoryDataService dataService)
    {
        _dataService = dataService;
        _historyStack = new Stack<string>();
    }

    // Save slots and metadata
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<MyBook.Models.SaveEntryMetadata> _saveSlots = new();

    [ObservableProperty]
    private string? _currentSaveSlot;

   
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

   
    public async Task InitializeAsync()
    {
        await _dataService.InitializeDatabaseAsync();
        var chapters = await _dataService.GetChaptersAsync();
        AvailableChapters = chapters;
        if (chapters.Any())
        {
            await LoadChapterAsync(chapters.First().Id);
        }
        // load existing save slots
        try { await RefreshSaveSlotsAsync(); } catch { }
    }

  
    public async Task LoadChapterAsync(string chapterId)
    {
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
        
        // 启动打字机效果
        StartTypewriterEffect(value?.Text ?? string.Empty);
        
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
    /// 启动打字机效果
    /// </summary>
    private void StartTypewriterEffect(string text)
    {
        // 如果正在读档中，跳过打字机效果
        if (_suppressTypewriter)
        {
            DisplayedText = text;
            IsTyping = false;
            return;
        }
        
        if (!EnableTypewriter || string.IsNullOrEmpty(text))
        {
            // 不使用打字机效果，直接显示
            DisplayedText = text;
            IsTyping = false;
            return;
        }

        // 先停止任何正在进行的打字机效果
        _typewriter.Complete();
        
        IsTyping = true;
        _ = _typewriter.StartTypingAsync(
            text,
            displayed => 
            {
                // 在UI线程更新
                Avalonia.Threading.Dispatcher.UIThread.Post(() => DisplayedText = displayed);
            },
            () => 
            {
                // 完成回调
                Avalonia.Threading.Dispatcher.UIThread.Post(() => IsTyping = false);
            }
        );
    }

    /// <summary>
    /// 跳过打字机效果，立即显示完整文本
    /// </summary>
    [RelayCommand]
    private void SkipTypewriter()
    {
        if (IsTyping)
        {
            _typewriter.Complete();
            DisplayedText = _typewriter.FullText;
            IsTyping = false;
        }
    }

    public bool CanNext => CurrentNode?.NextId != null && !HasVisibleChoices;

    
    public bool CanPrev => CurrentNode?.PrevId != null && !HasVisibleChoices;

  
    public bool HasChoices => CurrentNode?.Choices.Count > 0;

    public System.Collections.Generic.IEnumerable<MyBook.Models.StoryChoice> VisibleChoices
    {
        get
        {
            if (CurrentNode?.Choices == null) return System.Linq.Enumerable.Empty<MyBook.Models.StoryChoice>();
            return CurrentNode.Choices.Where(c => {
                var t = c.Text ?? string.Empty;
                if (t.IndexOf("继续", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (c.IsOneTime && !string.IsNullOrWhiteSpace(c.Id) && _chosenChoiceIds.Contains(c.Id)) return false;
              
                return true;
            });
        }
    }

    public bool HasVisibleChoices => VisibleChoices.Any();


    public bool CanRestart => !HasVisibleChoices;

   
    public bool CanGoToNextChapter
    {
        get
        {

            if (CurrentNode == null) return false;
            bool hasVisibleChoices = false;
            if (CurrentNode.Choices != null)
            {
                hasVisibleChoices = CurrentNode.Choices.Any(c => {
                    var t = c.Text ?? string.Empty;
                    return t.IndexOf("继续", StringComparison.OrdinalIgnoreCase) < 0;
                });
            }
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
        if (HasVisibleChoices) return; 
        if (CurrentNode?.NextId != null)
        {
            await NavigateToNodeAsync(CurrentNode.NextId);
        }
    }


    [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
    private async Task GoToNextChapterAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentChapterId)) return;

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


    [RelayCommand]
    private async Task SelectChapterAsync(string? chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId)) return;
        await LoadChapterAsync(chapterId);
    }

   
    [RelayCommand(CanExecute = nameof(CanPrev))]
    private async Task PrevAsync()
    {
        if (HasVisibleChoices) return; 
        if (CurrentNode?.PrevId != null)
        {
            await NavigateToNodeAsync(CurrentNode.PrevId);
        }
    }

 
    [RelayCommand]
    private async Task NavigateAsync(string? targetNodeId)
    {
        if (targetNodeId != null)
        {
            await NavigateToNodeAsync(targetNodeId);
        }
    }

 
    [RelayCommand]
    private async Task ChooseAsync(MyBook.Models.StoryChoice? choice)
    {
        if (choice == null)
        {
            FileLogger.Log("ChooseAsync called with null choice");
            return;
        }

        FileLogger.Log($"ChooseAsync invoked: Id={choice.Id}, TargetChapterId={choice.TargetChapterId}, TargetNodeId={choice.TargetNodeId}");

    
        try
        {
            if (!string.IsNullOrWhiteSpace(choice.Id)) _chosenChoiceIds.Add(choice.Id);
        }
        catch (Exception ex)
        {
            FileLogger.Log($"Error recording chosen id: {ex}");
        }

      
        if (!string.IsNullOrWhiteSpace(choice.TargetNodeId))
        {
            await NavigateToNodeAsync(choice.TargetNodeId);
            try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = choice.TargetNodeId }); } catch { }
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice.TargetChapterId))
        {
            try
            {
                var possibleChapter = await _dataService.GetChapterAsync(choice.TargetChapterId);
                if (possibleChapter != null)
                {
                    await LoadChapterAsync(choice.TargetChapterId);
                   
                    try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = choice.TargetChapterId }); } catch { }
                    return;
                }
            }
            catch { }

         
            await NavigateToNodeAsync(choice.TargetChapterId);
            try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = choice.TargetChapterId }); } catch { }
            return;
        }
       
        try { await _dataService.SaveGameStateAsync("default", new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id }); } catch { }
    }

  
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

    [RelayCommand]
    public async Task RefreshSaveSlotsAsync()
    {
        try
        {
            var entries = await _dataService.ListSaveSlotsAsync();
            SaveSlots.Clear();
            foreach (var e in entries)
            {
                SaveSlots.Add(e);
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"RefreshSaveSlotsAsync error: {ex}");
        }
    }

    [RelayCommand]
    private async Task QuickSaveAsync()
    {
        var slot = "quicksave";
        try
        {
            var entry = new MyBook.Models.SaveEntry
            {
                Version = 1,
                Meta = new MyBook.Models.SaveEntryMeta { Slot = slot, Name = "QuickSave", UpdatedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                State = new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id },
                Context = new MyBook.Models.SaveEntryContext { CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id, BackgroundImage = BackgroundImage, CurrentBgmPath = _currentBgmPath }
            };
            await _dataService.SaveRawSlotAsync(slot, entry);
            await RefreshSaveSlotsAsync();
        }
        catch (Exception ex)
        {
            FileLogger.Log($"QuickSaveAsync error: {ex}");
        }
    }

    [RelayCommand]
    private async Task QuickLoadAsync()
    {
        var slot = "quicksave";
        try
        {
            var entry = await _dataService.LoadRawSlotAsync(slot);
            if (entry == null) return;
            
            // 开始读档，抑制打字机效果直到最终节点
            _suppressTypewriter = true;
            
            // restore chosen ids
            _chosenChoiceIds.Clear();
            if (entry.State?.ChosenChoiceIds != null)
            {
                foreach (var id in entry.State.ChosenChoiceIds) _chosenChoiceIds.Add(id);
            }
            // restore simple state and navigate
            if (!string.IsNullOrWhiteSpace(entry.Context?.CurrentChapterId))
            {
                await LoadChapterAsync(entry.Context.CurrentChapterId);
            }
            
            // 关闭抑制，让最终的节点显示打字机效果
            _suppressTypewriter = false;
            
            if (!string.IsNullOrWhiteSpace(entry.Context?.CurrentNodeId))
            {
                await NavigateToNodeAsync(entry.Context.CurrentNodeId);
            }
        }
        catch (Exception ex)
        {
            _suppressTypewriter = false;
            FileLogger.Log($"QuickLoadAsync error: {ex}");
        }
    }

    public async Task SaveToSlotAsync(string slot, string name)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = $"slot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        try
        {
            var entry = new MyBook.Models.SaveEntry
            {
                Version = 1,
                Meta = new MyBook.Models.SaveEntryMeta { Slot = slot, Name = name ?? slot, UpdatedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                State = new MyBook.Models.GameState { ChosenChoiceIds = _chosenChoiceIds.ToList(), Variables = new Dictionary<string, string>(), CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id },
                Context = new MyBook.Models.SaveEntryContext { CurrentChapterId = _currentChapterId, CurrentNodeId = CurrentNode?.Id, BackgroundImage = BackgroundImage, CurrentBgmPath = _currentBgmPath }
            };
            await _dataService.SaveRawSlotAsync(slot, entry);
            CurrentSaveSlot = slot;
            await RefreshSaveSlotsAsync();
        }
        catch (Exception ex)
        {
            FileLogger.Log($"SaveToSlotAsync error: {ex}");
            throw;
        }
    }

    public async Task LoadSlotAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        try
        {
            var entry = await _dataService.LoadRawSlotAsync(slot);
            if (entry == null) return;
            
            // 开始读档，抑制打字机效果直到最终节点
            _suppressTypewriter = true;
            
            // restore chosen ids
            _chosenChoiceIds.Clear();
            if (entry.State?.ChosenChoiceIds != null)
            {
                foreach (var id in entry.State.ChosenChoiceIds) _chosenChoiceIds.Add(id);
            }
            // restore variables
            try
            {
                if (entry.State?.Variables != null)
                {
                    foreach (var kv in entry.State.Variables)
                    {
                        // current implementation does not have VM-level Variables dictionary; if needed add later
                    }
                }
            }
            catch { }

            // navigate to chapter/node
            if (!string.IsNullOrWhiteSpace(entry.Context?.CurrentChapterId))
            {
                await LoadChapterAsync(entry.Context.CurrentChapterId);
            }
            
            // 关闭抑制，让最终的节点显示打字机效果
            _suppressTypewriter = false;
            
            if (!string.IsNullOrWhiteSpace(entry.Context?.CurrentNodeId))
            {
                await NavigateToNodeAsync(entry.Context.CurrentNodeId);
            }
            CurrentSaveSlot = slot;
        }
        catch (Exception ex)
        {
            _suppressTypewriter = false;
            FileLogger.Log($"LoadSlotAsync error: {ex}");
            throw;
        }
    }

    public async Task DeleteSaveSlotAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        try
        {
            await _dataService.DeleteSaveSlotAsync(slot);
            if (string.Equals(CurrentSaveSlot, slot, StringComparison.OrdinalIgnoreCase))
            {
                CurrentSaveSlot = null;
            }
            await RefreshSaveSlotsAsync();
        }
        catch (Exception ex)
        {
            FileLogger.Log($"DeleteSaveSlotAsync error: {ex}");
            throw;
        }
    }
}
