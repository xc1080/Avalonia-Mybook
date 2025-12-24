namespace MyBook.Services;

using System.Collections.Generic;
using System.Threading.Tasks;
using MyBook.Models;


public interface IStoryDataService
{
    Task<List<Chapter>> GetChaptersAsync();
    Task<Chapter?> GetChapterAsync(string chapterId);
    Task SaveChapterAsync(Chapter chapter);
    Task DeleteChapterAsync(string chapterId);
    
    Task<List<StoryNodeExtended>> GetNodesAsync(string chapterId);
    Task<StoryNodeExtended?> GetNodeAsync(string nodeId);
    Task SaveNodeAsync(StoryNodeExtended node, bool flush = false);
    Task DeleteNodeAsync(string nodeId);
    
    Task ImportFromTextAsync(string text, string chapterId);
    Task<string> ExportToTextAsync(string chapterId);
    
    Task InitializeDatabaseAsync();
    
    Task SaveGameStateAsync(string slot, MyBook.Models.GameState state);
    Task<MyBook.Models.GameState?> LoadGameStateAsync(string slot);
    
    // Extended save/load APIs for structured save entries (meta + state + context)
    Task SaveRawSlotAsync(string slot, MyBook.Models.SaveEntry entry);
    Task<MyBook.Models.SaveEntry?> LoadRawSlotAsync(string slot);
    Task<List<MyBook.Models.SaveEntryMetadata>> ListSaveSlotsAsync();
    Task DeleteSaveSlotAsync(string slot);
}
