namespace MyBook.Models;


public class StoryChoice
{
  
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    public string Text { get; set; } = string.Empty;

  
    public string TargetNodeId { get; set; } = string.Empty;


    public string? TargetChapterId { get; set; }

    
    public string? Condition { get; set; }
    
 
    public bool IsOneTime { get; set; } = false;
}
