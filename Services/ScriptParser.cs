namespace MyBook.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyBook.Models;


public class ScriptParser
{

    public async Task<(Chapter chapter, List<StoryNodeExtended> nodes)> ParseScriptAsync(string script, string chapterId)
    {
        await Task.Yield();
        // Normalize lines and strip Markdown code-fence blocks if present
        var rawLines = script.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (rawLines.Count > 0 && rawLines[0].TrimStart().StartsWith("```"))
        {
            // remove opening fence
            rawLines.RemoveAt(0);
            // remove trailing fence if present
            if (rawLines.Count > 0 && rawLines[^1].Trim().StartsWith("```"))
            {
                rawLines.RemoveAt(rawLines.Count - 1);
            }
        }
        var lines = rawLines.ToArray();
        var chapter = new Chapter { Id = chapterId, Title = "\u672a\u547d\u540d\u7ae0\u8282", OrderIndex = 0 };
        var nodes = new List<StoryNodeExtended>();
        
        StoryNodeExtended? currentNode = null;
        var currentText = new List<string>();
        int nodeIndex = 0;
        bool foundTitle = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            
            if (line.StartsWith("# ") && !line.StartsWith("## "))
            {
                chapter.Title = line.Substring(2).Trim();
                foundTitle = true;
                continue;
            }

            
            if (!foundTitle && !string.IsNullOrWhiteSpace(line) && !line.StartsWith("##"))
            {
                chapter.Title = line.Length > 20 ? line.Substring(0, 20) + "..." : line;
                foundTitle = true;
            }

            
            if (line.StartsWith("## [") && line.Contains("]"))
            {
                
                if (currentNode != null)
                {
                    var text = string.Join("\n", currentText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        currentNode.Text = text;
                        nodes.Add(currentNode);
                    }
                    currentText.Clear();
                }

                
                var nodeIdMatch = Regex.Match(line, @"##\s*\[(.+?)\]");
                var rawId = nodeIdMatch.Success ? nodeIdMatch.Groups[1].Value : null;
                var nodeId = rawId != null
                    ? (rawId.StartsWith(chapterId + "_") ? rawId : $"{chapterId}_{rawId}")
                    : $"{chapterId}_node_{nodeIndex:D4}";

                currentNode = new StoryNodeExtended
                {
                    Id = nodeId,
                    ChapterId = chapterId,
                    Type = NodeType.Narration,
                    OrderIndex = nodeIndex++
                };
                continue;
            }

            
            if ((line.StartsWith("→ ") || line.StartsWith("- ")) && currentNode != null)
            {
                var choiceMatch = Regex.Match(line, @"[→-]\s*(.+?)\s*[→\[]\s*(.+?)\]");
                if (choiceMatch.Success)
                {
                    currentNode.Type = NodeType.Choice;
                    currentNode.Choices.Add(new StoryChoice
                    {
                        Text = choiceMatch.Groups[1].Value.Trim(),
                        TargetNodeId = choiceMatch.Groups[2].Value.Trim()
                    });
                }
                continue;
            }

            
            if (line.StartsWith("继续 → ") || line.StartsWith("next:"))
            {
                var nextMatch = Regex.Match(line, @"\[(.+?)\]");
                if (nextMatch.Success && currentNode != null)
                {
                    currentNode.NextId = nextMatch.Groups[1].Value;
                }
                continue;
            }

            
            if (line.StartsWith("背景:") || line.StartsWith("@background:"))
            {
                if (currentNode != null)
                {
                    currentNode.Visuals ??= new VisualData();
                    currentNode.Visuals.BackgroundImage = line.Split(':', 2)[1].Trim();
                }
                continue;
            }

            if (line.StartsWith("音乐:") || line.StartsWith("@bgm:"))
            {
                if (currentNode != null)
                {
                    currentNode.Audio ??= new AudioData();
                    currentNode.Audio.BgmFile = line.Split(':', 2)[1].Trim();
                }
                continue;
            }

            
            if (line == "---" || line == "***")
            {
                if (currentNode != null)
                {
                    currentNode.Text = string.Join("\n", currentText).Trim();
                    nodes.Add(currentNode);
                    currentText.Clear();
                    currentNode = null;
                }
                continue;
            }

            
            if (currentNode != null)
            {
                currentText.Add(line);
            }
        }

        
        if (currentNode != null)
        {
            var text = string.Join("\n", currentText).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                currentNode.Text = text;
                nodes.Add(currentNode);
            }
        }

        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i > 0 && nodes[i].PrevId == null && nodes[i].Type != NodeType.Choice)
            {
                nodes[i].PrevId = nodes[i - 1].Id;
            }
            if (i < nodes.Count - 1 && nodes[i].NextId == null && nodes[i].Type != NodeType.Choice)
            {
                nodes[i].NextId = nodes[i + 1].Id;
            }
        }

        return (chapter, nodes);
    }

  
    public async Task<List<StoryNodeExtended>> ParseSimpleTextAsync(string text, string chapterId)
    {
        await Task.Yield();
        var nodes = new List<StoryNodeExtended>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < paragraphs.Length; i++)
        {
            var paragraph = paragraphs[i].Trim();
            if (string.IsNullOrWhiteSpace(paragraph)) continue;

            var node = new StoryNodeExtended
            {
                Id = $"{chapterId}_node_{i:D4}",
                ChapterId = chapterId,
                Type = NodeType.Narration,
                Text = paragraph,
                OrderIndex = i,
                NextId = i < paragraphs.Length - 1 ? $"{chapterId}_node_{i + 1:D4}" : null,
                PrevId = i > 0 ? $"{chapterId}_node_{i - 1:D4}" : null
            };

            nodes.Add(node);
        }

        return nodes;
    }
}
