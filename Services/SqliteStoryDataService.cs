using System.Linq;

namespace MyBook.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MyBook.Models;

public class SqliteStoryDataService : IStoryDataService
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _semaphore = new(1,1);

    public SqliteStoryDataService(string? folder = null)
    {
        var baseDir = AppContext.BaseDirectory;
        var storiesDir = string.IsNullOrWhiteSpace(folder) ? Path.Combine(baseDir, "Stories") : Path.Combine(baseDir, folder);
        if (!Directory.Exists(storiesDir)) Directory.CreateDirectory(storiesDir);
        _dbPath = Path.Combine(storiesDir, "story_data.db");
    }

    public async Task InitializeDatabaseAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Chapters (
  Id TEXT PRIMARY KEY,
  Title TEXT,
  OrderIndex INTEGER,
  Description TEXT
);
CREATE TABLE IF NOT EXISTS Nodes (
  Id TEXT PRIMARY KEY,
  ChapterId TEXT,
  Type INTEGER,
  Speaker TEXT,
  Text TEXT,
  OrderIndex INTEGER,
  NextId TEXT,
  PrevId TEXT,
  Visuals TEXT,
  Audio TEXT,
  Choices TEXT
);
CREATE TABLE IF NOT EXISTS GameStates (
    Slot TEXT PRIMARY KEY,
    Data TEXT,
    UpdatedAt TEXT
);
";
            await cmd.ExecuteNonQueryAsync();
                        // Migration: convert empty-string JSON columns to NULL so deserialization is safe
                        var cleanup = conn.CreateCommand();
                        cleanup.CommandText = @"
UPDATE Nodes SET Visuals = NULL WHERE Visuals = '';
UPDATE Nodes SET Audio = NULL WHERE Audio = '';
UPDATE Nodes SET Choices = NULL WHERE Choices = '';
";
                        await cleanup.ExecuteNonQueryAsync();
                        // Also ensure GameStates table exists and clean up
                        var cleanup2 = conn.CreateCommand();
                        cleanup2.CommandText = @"
UPDATE GameStates SET Data = NULL WHERE Data = '';
";
                        try { await cleanup2.ExecuteNonQueryAsync(); } catch { }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Chapter>> GetChaptersAsync()
    {
        await InitializeDatabaseAsync();
        var list = new List<Chapter>();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Title, OrderIndex, Description FROM Chapters ORDER BY OrderIndex";
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new Chapter
                {
                    Id = rdr.GetString(0),
                    Title = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                    OrderIndex = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                    Description = rdr.IsDBNull(3) ? null : rdr.GetString(3)
                });
            }
        }
        finally
        {
            _semaphore.Release();
        }
        return list;
    }

    public async Task<Chapter?> GetChapterAsync(string chapterId)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Title, OrderIndex, Description FROM Chapters WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", chapterId);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Chapter
                {
                    Id = rdr.GetString(0),
                    Title = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                    OrderIndex = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                    Description = rdr.IsDBNull(3) ? null : rdr.GetString(3)
                };
            }
        }
        finally
        {
            _semaphore.Release();
        }
        return null;
    }

    public async Task SaveChapterAsync(Chapter chapter)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Chapters (Id, Title, OrderIndex, Description)
VALUES ($id, $title, $idx, $desc)
ON CONFLICT(Id) DO UPDATE SET Title=$title, OrderIndex=$idx, Description=$desc";
            cmd.Parameters.AddWithValue("$id", chapter.Id);
            cmd.Parameters.AddWithValue("$title", chapter.Title ?? string.Empty);
            cmd.Parameters.AddWithValue("$idx", chapter.OrderIndex);
            cmd.Parameters.AddWithValue("$desc", chapter.Description ?? string.Empty);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteChapterAsync(string chapterId)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var t = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Nodes WHERE ChapterId = $cid";
            cmd.Parameters.AddWithValue("$cid", chapterId);
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "DELETE FROM Chapters WHERE Id = $cid";
            await cmd.ExecuteNonQueryAsync();
            await t.CommitAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<StoryNodeExtended>> GetNodesAsync(string chapterId)
    {
        await InitializeDatabaseAsync();
        var list = new List<StoryNodeExtended>();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, ChapterId, Type, Speaker, Text, OrderIndex, NextId, PrevId, Visuals, Audio, Choices FROM Nodes WHERE ChapterId = $cid ORDER BY OrderIndex";
            cmd.Parameters.AddWithValue("$cid", chapterId);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var node = new StoryNodeExtended
                {
                    Id = rdr.GetString(0),
                    ChapterId = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                    Type = (NodeType)(rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2)),
                    Speaker = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    Text = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                    OrderIndex = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5),
                    NextId = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    PrevId = rdr.IsDBNull(7) ? null : rdr.GetString(7)
                };
                if (!rdr.IsDBNull(8))
                {
                    var s = rdr.GetString(8);
                    if (!string.IsNullOrWhiteSpace(s)) node.Visuals = JsonSerializer.Deserialize<VisualData>(s);
                }
                if (!rdr.IsDBNull(9))
                {
                    var s = rdr.GetString(9);
                    if (!string.IsNullOrWhiteSpace(s)) node.Audio = JsonSerializer.Deserialize<AudioData>(s);
                }
                if (!rdr.IsDBNull(10))
                {
                    var s = rdr.GetString(10);
                    if (!string.IsNullOrWhiteSpace(s)) node.Choices = JsonSerializer.Deserialize<List<StoryChoice>>(s) ?? new List<StoryChoice>();
                    else node.Choices = new List<StoryChoice>();
                }
                list.Add(node);
            }
        }
        finally
        {
            _semaphore.Release();
        }
        return list;
    }

    public async Task<StoryNodeExtended?> GetNodeAsync(string nodeId)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, ChapterId, Type, Speaker, Text, OrderIndex, NextId, PrevId, Visuals, Audio, Choices FROM Nodes WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                var node = new StoryNodeExtended
                {
                    Id = rdr.GetString(0),
                    ChapterId = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                    Type = (NodeType)(rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2)),
                    Speaker = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    Text = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                    OrderIndex = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5),
                    NextId = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    PrevId = rdr.IsDBNull(7) ? null : rdr.GetString(7)
                };
                if (!rdr.IsDBNull(8))
                {
                    var s = rdr.GetString(8);
                    if (!string.IsNullOrWhiteSpace(s)) node.Visuals = JsonSerializer.Deserialize<VisualData>(s);
                }
                if (!rdr.IsDBNull(9))
                {
                    var s = rdr.GetString(9);
                    if (!string.IsNullOrWhiteSpace(s)) node.Audio = JsonSerializer.Deserialize<AudioData>(s);
                }
                if (!rdr.IsDBNull(10))
                {
                    var s = rdr.GetString(10);
                    if (!string.IsNullOrWhiteSpace(s)) node.Choices = JsonSerializer.Deserialize<List<StoryChoice>>(s) ?? new List<StoryChoice>();
                    else node.Choices = new List<StoryChoice>();
                }
                return node;
            }
        }
        finally
        {
            _semaphore.Release();
        }
        return null;
    }

    public async Task SaveNodeAsync(StoryNodeExtended node, bool flush = false)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var csCheck = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using (var connCheck = new SqliteConnection(csCheck))
            {
                await connCheck.OpenAsync();
                using var checkCmd = connCheck.CreateCommand();
                checkCmd.CommandText = "SELECT ChapterId FROM Nodes WHERE Id = $id";
                checkCmd.Parameters.AddWithValue("$id", node.Id);
                var existing = await checkCmd.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value)
                {
                    var existingChapterId = existing.ToString();
                    if (!string.Equals(existingChapterId, node.ChapterId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!node.Id.StartsWith(node.ChapterId + "_"))
                        {
                            node.Id = $"{node.ChapterId}_{node.Id}";
                        }
                    }
                }
            }

            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Nodes (Id, ChapterId, Type, Speaker, Text, OrderIndex, NextId, PrevId, Visuals, Audio, Choices)
VALUES ($id,$cid,$type,$speaker,$text,$idx,$next,$prev,$visuals,$audio,$choices)
ON CONFLICT(Id) DO UPDATE SET ChapterId=$cid, Type=$type, Speaker=$speaker, Text=$text, OrderIndex=$idx, NextId=$next, PrevId=$prev, Visuals=$visuals, Audio=$audio, Choices=$choices";
            cmd.Parameters.AddWithValue("$id", node.Id);
            cmd.Parameters.AddWithValue("$cid", string.IsNullOrWhiteSpace(node.ChapterId) ? DBNull.Value : (object)node.ChapterId);
            cmd.Parameters.AddWithValue("$type", (int)node.Type);
            cmd.Parameters.AddWithValue("$speaker", node.Speaker ?? string.Empty);
            cmd.Parameters.AddWithValue("$text", node.Text ?? string.Empty);
            cmd.Parameters.AddWithValue("$idx", node.OrderIndex);
            cmd.Parameters.AddWithValue("$next", string.IsNullOrWhiteSpace(node.NextId) ? DBNull.Value : (object)node.NextId);
            cmd.Parameters.AddWithValue("$prev", string.IsNullOrWhiteSpace(node.PrevId) ? DBNull.Value : (object)node.PrevId);
            cmd.Parameters.AddWithValue("$visuals", node.Visuals == null ? DBNull.Value : (object)JsonSerializer.Serialize(node.Visuals));
            cmd.Parameters.AddWithValue("$audio", node.Audio == null ? DBNull.Value : (object)JsonSerializer.Serialize(node.Audio));
            cmd.Parameters.AddWithValue("$choices", node.Choices == null || node.Choices.Count == 0 ? DBNull.Value : (object)JsonSerializer.Serialize(node.Choices));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteNodeAsync(string nodeId)
    {
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Nodes WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", nodeId);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ImportFromTextAsync(string text, string chapterId)
    {
        var parser = new ScriptParser();
        var (chapter, nodes) = await parser.ParseScriptAsync(text, chapterId);
        chapter.Id = chapterId;
        await SaveChapterAsync(chapter);
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].ChapterId = chapterId;
            await SaveNodeAsync(nodes[i], true);
        }
    }

    public async Task<string> ExportToTextAsync(string chapterId)
    {
        var nodes = await GetNodesAsync(chapterId);
        var parts = new List<string>();
        foreach (var n in nodes)
        {
            parts.Add(n.Text);
        }
        return string.Join("\n\n", parts);
    }

    public async Task SaveGameStateAsync(string slot, MyBook.Models.GameState state)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = "default";
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO GameStates (Slot, Data, UpdatedAt) VALUES ($slot,$data,$updated)
ON CONFLICT(Slot) DO UPDATE SET Data=$data, UpdatedAt=$updated";
            cmd.Parameters.AddWithValue("$slot", slot);
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            cmd.Parameters.AddWithValue("$data", string.IsNullOrWhiteSpace(json) ? DBNull.Value : (object)json);
            cmd.Parameters.AddWithValue("$updated", System.DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MyBook.Models.GameState?> LoadGameStateAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = "default";
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM GameStates WHERE Slot = $slot";
            cmd.Parameters.AddWithValue("$slot", slot);
            var res = await cmd.ExecuteScalarAsync();
            if (res == null || res == DBNull.Value) return null;
            var s = res.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<MyBook.Models.GameState>(s);
            }
            catch { return null; }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // New: Save structured SaveEntry into GameStates.Data as JSON (backward-compatible)
    public async Task SaveRawSlotAsync(string slot, MyBook.Models.SaveEntry entry)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = "default";
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO GameStates (Slot, Data, UpdatedAt) VALUES ($slot,$data,$updated)
ON CONFLICT(Slot) DO UPDATE SET Data=$data, UpdatedAt=$updated";
            cmd.Parameters.AddWithValue("$slot", slot);
            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            cmd.Parameters.AddWithValue("$data", string.IsNullOrWhiteSpace(json) ? DBNull.Value : (object)json);
            cmd.Parameters.AddWithValue("$updated", System.DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MyBook.Models.SaveEntry?> LoadRawSlotAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = "default";
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM GameStates WHERE Slot = $slot";
            cmd.Parameters.AddWithValue("$slot", slot);
            var res = await cmd.ExecuteScalarAsync();
            if (res == null || res == DBNull.Value) return null;
            var s = res.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<MyBook.Models.SaveEntry>(s);
            }
            catch
            {
                return null;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MyBook.Models.SaveEntryMetadata>> ListSaveSlotsAsync()
    {
        var list = new List<MyBook.Models.SaveEntryMetadata>();
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Slot, Data, UpdatedAt FROM GameStates";
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var slotName = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                var data = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                var updated = rdr.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(rdr.GetString(2));
                if (!string.IsNullOrWhiteSpace(data))
                {
                    try
                    {
                        var entry = System.Text.Json.JsonSerializer.Deserialize<MyBook.Models.SaveEntry>(data);
                        if (entry != null)
                        {
                            var s = entry.Meta?.Slot ?? slotName;
                            if (string.IsNullOrWhiteSpace(s)) continue; // skip invalid slot
                            list.Add(new MyBook.Models.SaveEntryMetadata
                            {
                                Slot = s,
                                Name = entry.Meta?.Name ?? entry.Meta?.Slot ?? slotName,
                                UpdatedAt = entry.Meta?.UpdatedAt ?? updated,
                                Version = entry.Version,
                                ThumbnailLength = string.IsNullOrWhiteSpace(entry.Meta?.ThumbnailBase64) ? null : (int?)entry.Meta!.ThumbnailBase64!.Length,
                                ShortDescription = entry.Context?.CurrentChapterId
                            });
                            continue;
                        }
                    }
                    catch { }
                }
                // fallback minimal metadata
                if (string.IsNullOrWhiteSpace(slotName)) continue; // skip rows without slot id
                list.Add(new MyBook.Models.SaveEntryMetadata
                {
                    Slot = slotName,
                    Name = slotName,
                    UpdatedAt = updated,
                    Version = 0,
                    ThumbnailLength = null,
                    ShortDescription = null
                });
            }
        }
        finally
        {
            _semaphore.Release();
        }
        return list.OrderByDescending(m => m.UpdatedAt).ToList();
    }

    public async Task DeleteSaveSlotAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) slot = "default";
        await InitializeDatabaseAsync();
        await _semaphore.WaitAsync();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
            using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GameStates WHERE Slot = $slot";
            cmd.Parameters.AddWithValue("$slot", slot);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
