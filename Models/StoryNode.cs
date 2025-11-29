namespace MyBook.Models;

using System.Collections.Generic;


public class StoryNode
{
   
    public string Id { get; set; } = string.Empty;
    
    
    public string Text { get; set; } = string.Empty;
    
   
    public List<StoryChoice> Choices { get; set; } = new();
    
       public string? NextId { get; set; }
    
    
    public string? PrevId { get; set; }
    
   
    public bool IsEnding { get; set; } = false;

    public string? EndingCondition { get; set; }

    
    public string? Visuals { get; set; }

   
    public string? Audio { get; set; }

   
    public bool AutoPlayAudio { get; set; } = false;
}
