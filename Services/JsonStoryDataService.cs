namespace MyBook.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyBook.Models;


public class JsonStoryDataService : IStoryDataService
{
    private readonly string _dataPath;
    private Dictionary<string, Chapter> _chapters = new();
    private Dictionary<string, StoryNodeExtended> _nodes = new();

    private readonly string _resolvedDataPath;
    private readonly object _saveCtsLock = new();
    private CancellationTokenSource? _saveCts;
    private const int DefaultSaveDebounceMs = 300;
    private readonly System.Threading.SemaphoreSlim _saveSemaphore = new(1, 1);
    private DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan MinImmediateSaveInterval = TimeSpan.FromMilliseconds(150);

    public JsonStoryDataService(string dataPath = "Stories")
    {
        _dataPath = dataPath;
        string candidateResolved;
        if (Path.IsPathRooted(_dataPath))
        {
            candidateResolved = _dataPath;
        }
        else
        {
            candidateResolved = Path.Combine(AppContext.BaseDirectory, _dataPath);
        }

        try
        {
            var projectStoriesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Stories"));
            if (Directory.Exists(projectStoriesDir))
            {
                _resolvedDataPath = projectStoriesDir;
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Using project-level Stories folder: {_resolvedDataPath}");
                WriteLog($"Using project-level Stories folder: {_resolvedDataPath}");
            }
            else
            {
                _resolvedDataPath = candidateResolved;
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Data folder resolved to: {_resolvedDataPath}");
                WriteLog($"Data folder resolved to: {_resolvedDataPath}");
            }
        }
        catch
        {
            _resolvedDataPath = candidateResolved;
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Data folder resolved to: {_resolvedDataPath}");
            WriteLog($"Data folder resolved to: {_resolvedDataPath}");
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            if (!Directory.Exists(_resolvedDataPath))
            {
                Directory.CreateDirectory(_resolvedDataPath);
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Created data directory: {_resolvedDataPath}");
            }

            var storyFile = Path.Combine(_resolvedDataPath, "story_data.json");
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Looking for story file at: {storyFile}");
            WriteLog($"Looking for story file at: {storyFile}");
            if (File.Exists(storyFile))
            {
                await LoadFromFileAsync(storyFile);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] story_data.json does not exist yet at: {storyFile}");
                WriteLog($"story_data.json does not exist yet at: {storyFile}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] InitializeDatabaseAsync error: {ex}");
            WriteLog($"InitializeDatabaseAsync error: {ex}");
            throw;
        }
    }

    public Task<List<Chapter>> GetChaptersAsync()
    {
        return Task.FromResult(_chapters.Values.OrderBy(c => c.OrderIndex).ToList());
    }

    public Task<Chapter?> GetChapterAsync(string chapterId)
    {
        _chapters.TryGetValue(chapterId, out var chapter);
        return Task.FromResult(chapter);
    }

    public async Task SaveChapterAsync(Chapter chapter)
    {
        _chapters[chapter.Id] = chapter;
        ScheduleSave();
    }

    public async Task DeleteChapterAsync(string chapterId)
    {
        _chapters.Remove(chapterId);
        
        var nodesToRemove = _nodes.Where(n => n.Value.ChapterId == chapterId)
                                  .Select(n => n.Key)
                                  .ToList();
        foreach (var nodeId in nodesToRemove)
        {
            _nodes.Remove(nodeId);
        }
        
        ScheduleSave();
    }

    public Task<List<StoryNodeExtended>> GetNodesAsync(string chapterId)
    {
        var nodes = _nodes.Values
            .Where(n => n.ChapterId == chapterId)
            .OrderBy(n => n.OrderIndex)
            .ToList();
        return Task.FromResult(nodes);
    }

    public Task<StoryNodeExtended?> GetNodeAsync(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    public async Task SaveNodeAsync(StoryNodeExtended node, bool flush = false)
    {
        _nodes[node.Id] = node;
        if (flush)
        {
            await SaveToFileAsync();
        }
        else
        {
            ScheduleSave();
        }
    }

    public async Task DeleteNodeAsync(string nodeId)
    {
        _nodes.Remove(nodeId);
        ScheduleSave();
    }

    public async Task ImportFromTextAsync(string text, string chapterId)
    {
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < paragraphs.Length; i++)
        {
            var node = new StoryNodeExtended
            {
                Id = $"{chapterId}_node_{i:D4}",
                ChapterId = chapterId,
                Type = NodeType.Narration,
                Text = paragraphs[i].Trim(),
                OrderIndex = i,
                NextId = i < paragraphs.Length - 1 ? $"{chapterId}_node_{i + 1:D4}" : null,
                PrevId = i > 0 ? $"{chapterId}_node_{i - 1:D4}" : null
            };

            _nodes[node.Id] = node;
        }
        
        await SaveToFileAsync();
    }

    public async Task<string> ExportToTextAsync(string chapterId)
    {
        var nodes = await GetNodesAsync(chapterId);
        return string.Join("\n\n", nodes.Select(n => n.Text));
    }

    private async Task LoadFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Loaded story file ({filePath}), length={json?.Length}");
            WriteLog($"Loaded story file ({filePath}), length={json?.Length}");
            var data = JsonSerializer.Deserialize<StoryData>(json);

            if (data != null)
            {
                _chapters = data.Chapters.ToDictionary(c => c.Id);
                _nodes = data.Nodes.ToDictionary(n => n.Id);
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Loaded { _chapters.Count } chapters and { _nodes.Count } nodes from file");
                WriteLog($"Loaded { _chapters.Count } chapters and { _nodes.Count } nodes from file");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[JsonStoryDataService] Deserialized story file returned null data");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] LoadFromFileAsync error: {ex}");
            WriteLog($"LoadFromFileAsync error: {ex}");
            throw;
        }
    }

    private async Task SaveToFileAsync()
    {
            
        await _saveSemaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastSaveTime) < MinImmediateSaveInterval)
            {
                WriteLog("Skipping rapid duplicate SaveToFileAsync");
                return;
            }

            var data = new StoryData
            {
                Chapters = _chapters.Values.ToList(),
                Nodes = _nodes.Values.ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);

            var storyFile = Path.Combine(_resolvedDataPath, "story_data.json");
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Saving story file to: {storyFile}, bytes={System.Text.Encoding.UTF8.GetByteCount(json)}");
            WriteLog($"Saving story file to: {storyFile}, bytes={System.Text.Encoding.UTF8.GetByteCount(json)}");
            await File.WriteAllTextAsync(storyFile, json);
            System.Diagnostics.Debug.WriteLine("[JsonStoryDataService] SaveToFileAsync completed successfully");
            WriteLog("SaveToFileAsync completed successfully");

            _lastSaveTime = DateTime.UtcNow;

            try
            {
                var projectStoriesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Stories"));
                if (!Directory.Exists(projectStoriesDir))
                {
                    Directory.CreateDirectory(projectStoriesDir);
                }
                var projectStoryFile = Path.Combine(projectStoriesDir, "story_data.json");
                await File.WriteAllTextAsync(projectStoryFile, json);
                WriteLog($"Also wrote project copy to: {projectStoryFile}");
                System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] Also wrote project copy to: {projectStoryFile}");
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to write project copy: {ex}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JsonStoryDataService] SaveToFileAsync error: {ex}");
            WriteLog($"SaveToFileAsync error: {ex}");
            throw;
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private void ScheduleSave(int delayMs = DefaultSaveDebounceMs)
    {
        try
        {
            lock (_saveCtsLock)
            {
                _saveCts?.Cancel();
                _saveCts = new CancellationTokenSource();
                var token = _saveCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delayMs, token);
                        if (!token.IsCancellationRequested)
                        {
                            await SaveToFileAsync();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"ScheduleSave error: {ex}");
                    }
                }, token);
            }
        }
        catch (Exception ex)
        {
            WriteLog($"ScheduleSave scheduling error: {ex}");
        }
    }

    private void WriteLog(string message)
    {
        try
        {
            var logFile = Path.Combine(_resolvedDataPath, "json_service.log");
            var line = $"{DateTime.Now:o} {message}{Environment.NewLine}";
            File.AppendAllText(logFile, line);
        }
        catch
        {
        }
    }

    private class StoryData
    {
        public List<Chapter> Chapters { get; set; } = new();
        public List<StoryNodeExtended> Nodes { get; set; } = new();
    }

    public async Task SaveGameStateAsync(string slot, MyBook.Models.GameState state)
    {
        try
        {
            var file = Path.Combine(_resolvedDataPath, $"game_state_{(string.IsNullOrWhiteSpace(slot) ? "default" : slot)}.json");
            var opts = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(state, opts);
            await File.WriteAllTextAsync(file, json);
        }
        catch (Exception ex)
        {
            WriteLog($"SaveGameStateAsync error: {ex}");
            throw;
        }
    }

    public async Task<MyBook.Models.GameState?> LoadGameStateAsync(string slot)
    {
        try
        {
            var file = Path.Combine(_resolvedDataPath, $"game_state_{(string.IsNullOrWhiteSpace(slot) ? "default" : slot)}.json");
            if (!File.Exists(file)) return null;
            var json = await File.ReadAllTextAsync(file);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<MyBook.Models.GameState>(json);
        }
        catch (Exception ex)
        {
            WriteLog($"LoadGameStateAsync error: {ex}");
            return null;
        }
    }
}
