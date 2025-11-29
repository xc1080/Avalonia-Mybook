namespace MyBook.Models;

using System.Collections.Generic;

public class GameState
{
    
    public List<string> ChosenChoiceIds { get; set; } = new();

    public Dictionary<string, string> Variables { get; set; } = new();

    public string? CurrentChapterId { get; set; }
    public string? CurrentNodeId { get; set; }
}
