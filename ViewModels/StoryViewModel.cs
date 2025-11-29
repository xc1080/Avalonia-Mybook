namespace MyBook.ViewModels;

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBook.Models;

/// <summary>
/// 现在不使用了
/// </summary>
public partial class StoryViewModel : ViewModelBase
{
    [ObservableProperty]
    private StoryNode? _currentNode;

    private readonly Dictionary<string, StoryNode> _storyMap;
    private readonly Stack<string> _historyStack;

    public StoryViewModel()
    {
        _historyStack = new Stack<string>();

        _storyMap = InitializeStory();

        CurrentNode = _storyMap["start"];
    }

  
    partial void OnCurrentNodeChanged(StoryNode? value)
    {
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(CanPrev));
        OnPropertyChanged(nameof(HasChoices));
        NextCommand.NotifyCanExecuteChanged();
        PrevCommand.NotifyCanExecuteChanged();
    }

 
    private Dictionary<string, StoryNode> InitializeStory()
    {
        var story = new Dictionary<string, StoryNode>();

        story["start"] = new StoryNode
        {
            Id = "start",
            Text = "欢迎来到视觉小说！\n\n你醒来时发现自己身处一片陌生的森林中。阳光透过树叶洒下，鸟鸣声在耳边回响。\n\n前方有两条截然不同的道路...",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "走左边阴暗的小路", TargetNodeId = "left_path" },
                new StoryChoice { Text = "走右边明亮的大道", TargetNodeId = "right_path" }
            }
        };

        story["left_path"] = new StoryNode
        {
            Id = "left_path",
            Text = "你选择了左边的小路。\n\n随着深入，周围越来越阴暗，树木变得扭曲而诡异。突然，你听到了奇怪的声音...",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "继续前进，探索声音的来源", TargetNodeId = "left_continue" },
                new StoryChoice { Text = "感到害怕，原路返回", TargetNodeId = "start" }
            },
            PrevId = "start"
        };

        story["left_continue"] = new StoryNode
        {
            Id = "left_continue",
            Text = "你鼓起勇气继续前进...\n\n在黑暗深处，你发现了一个神秘的洞穴，里面似乎藏着什么东西。但突然，一个黑影从洞中冲出！",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "面对黑影", TargetNodeId = "ending_bad" },
                new StoryChoice { Text = "逃跑", TargetNodeId = "ending_escape" }
            },
            PrevId = "left_path"
        };

        story["right_path"] = new StoryNode
        {
            Id = "right_path",
            Text = "你选择了右边的大道。\n\n阳光明媚，空气清新。走了一会儿，你看到远处有一座美丽的村庄，炊烟袅袅升起。",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "前往村庄", TargetNodeId = "village" },
                new StoryChoice { Text = "继续在森林中探索", TargetNodeId = "explore_forest" }
            },
            PrevId = "start"
        };

        story["village"] = new StoryNode
        {
            Id = "village",
            Text = "你来到了村庄。\n\n村民们热情地欢迎你，为你提供了食物和住所。你终于找到了归属感。",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "在村庄定居", TargetNodeId = "ending_good" }
            },
            PrevId = "right_path"
        };

        story["explore_forest"] = new StoryNode
        {
            Id = "explore_forest",
            Text = "你决定继续探索森林...\n\n在深处，你发现了一个隐藏的瀑布，景色美不胜收。这里似乎是个修行的好地方。",
            Choices = new List<StoryChoice>
            {
                new StoryChoice { Text = "在此隐居", TargetNodeId = "ending_hermit" }
            },
            PrevId = "right_path"
        };

        story["ending_bad"] = new StoryNode
        {
            Id = "ending_bad",
            Text = "黑影将你吞没...\n\n当你再次醒来时，发现自己已经成为了森林的一部分。\n\n【结局A：被黑暗吞噬】",
            IsEnding = true,
            PrevId = "left_continue"
        };

        story["ending_escape"] = new StoryNode
        {
            Id = "ending_escape",
            Text = "你拼命逃跑，最终逃出了森林。\n\n虽然保住了性命，但那段恐怖的经历将永远留在你的记忆中。\n\n【结局B：惊险逃脱】",
            IsEnding = true,
            PrevId = "left_continue"
        };

        story["ending_good"] = new StoryNode
        {
            Id = "ending_good",
            Text = "你在村庄定居下来。\n\n日子平静而美好，你找到了人生的意义和归属。这就是你一直寻找的幸福。\n\n【结局C：美好生活】",
            IsEnding = true,
            PrevId = "village"
        };

        story["ending_hermit"] = new StoryNode
        {
            Id = "ending_hermit",
            Text = "你选择在瀑布边隐居。\n\n远离尘嚣，与自然为伴。在这份宁静中，你找到了内心的平和。\n\n【结局D：隐士之路】",
            IsEnding = true,
            PrevId = "explore_forest"
        };

        return story;
    }


    public bool CanNext => CurrentNode?.NextId != null;

 
    public bool CanPrev => CurrentNode?.PrevId != null;

   
    public bool HasChoices => CurrentNode?.Choices.Count > 0;

  
    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next()
    {
        if (CurrentNode?.NextId != null && _storyMap.TryGetValue(CurrentNode.NextId, out var nextNode))
        {
            _historyStack.Push(CurrentNode.Id);
            CurrentNode = nextNode;
        }
    }

   
    [RelayCommand(CanExecute = nameof(CanPrev))]
    private void Prev()
    {
        if (CurrentNode?.PrevId != null && _storyMap.TryGetValue(CurrentNode.PrevId, out var prevNode))
        {
            CurrentNode = prevNode;
        }
    }

 
    [RelayCommand]
    private void Navigate(string? targetNodeId)
    {
        if (targetNodeId != null && _storyMap.TryGetValue(targetNodeId, out var targetNode))
        {
            if (CurrentNode != null)
            {
                _historyStack.Push(CurrentNode.Id);
            }
            CurrentNode = targetNode;
        }
    }

  
    [RelayCommand]
    private void Restart()
    {
        _historyStack.Clear();
        CurrentNode = _storyMap["start"];
    }
}
