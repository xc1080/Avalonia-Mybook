namespace MyBook.Models;

using System;
using System.Collections.Generic;

public class SaveEntry
{
    public int Version { get; set; } = 1;
    public SaveEntryMeta Meta { get; set; } = new();
    public GameState State { get; set; } = new();
    public SaveEntryContext Context { get; set; } = new();
}

public class SaveEntryMeta
{
    public string Slot { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    // Optional base64 thumbnail; may be null or empty
    public string? ThumbnailBase64 { get; set; }
}

public class SaveEntryContext
{
    public string? CurrentChapterId { get; set; }
    public string? CurrentNodeId { get; set; }
    public string? BackgroundImage { get; set; }
    public string? CurrentBgmPath { get; set; }
}

public class SaveEntryMetadata
{
    public string Slot { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public int? ThumbnailLength { get; set; }
    public string? ShortDescription { get; set; }
}
