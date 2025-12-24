namespace MyBook.Models;

using System.Collections.Generic;


public class Chapter
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string? Description { get; set; }
}


public class StoryNodeExtended
{
    public string Id { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public NodeType Type { get; set; } = NodeType.Dialogue;
    public string? Speaker { get; set; }
    public string Text { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    
    public string? NextId { get; set; }
    public string? PrevId { get; set; }
    public VisualData? Visuals { get; set; }
    public AudioData? Audio { get; set; }
    public List<StoryChoice> Choices { get; set; } = new();
        
        
        public string? EndingCondition { get; set; }

        public bool IsEnding => Type == NodeType.Ending;
}


public enum NodeType
{
    Dialogue,
    Choice,
    Ending,
    Narration
}


public class VisualData
{
    public string? BackgroundImage { get; set; }
    public TransitionType Transition { get; set; } = TransitionType.None;
}


public class AudioData
{
    public string? BgmFile { get; set; }
    public double BgmVolume { get; set; } = 0.8;
    public string? SeFile { get; set; }
    public string? VoiceFile { get; set; }
}


public enum TransitionType
{
    None,
    Fade,
    Dissolve,
    Wipe
}
