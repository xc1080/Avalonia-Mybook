using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace MyBook.Services;

/// <summary>
/// 从音频文件中截取片段的工具类
/// 支持提取多个音效变体，实现更自然的打字机音效
/// </summary>
public static class AudioClipExtractor
{
    // 音效变体数量
    private const int VariantCount = 5;
    
    /// <summary>
    /// 从 MP3 文件中找到所有按键声音的起始位置（毫秒）
    /// </summary>
    private static List<int> FindAllKeyPressesMs(string inputPath, float threshold = 0.03f, int minGapMs = 50)
    {
        var positions = new List<int>();
        using var reader = new AudioFileReader(inputPath);
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var samplesPerMs = sampleRate * channels / 1000;
        
        var buffer = new float[samplesPerMs * 5]; // 每次读取5ms
        int msOffset = 0;
        int lastFoundMs = -minGapMs; // 确保第一个能被找到
        bool inSound = false;
        
        while (reader.Read(buffer, 0, buffer.Length) > 0)
        {
            // 计算这段的峰值
            float peak = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                var abs = Math.Abs(buffer[i]);
                if (abs > peak) peak = abs;
            }
            
            // 检测声音开始（从静音到有声）
            if (!inSound && peak > threshold && (msOffset - lastFoundMs) >= minGapMs)
            {
                positions.Add(msOffset);
                lastFoundMs = msOffset;
                inSound = true;
            }
            else if (inSound && peak < threshold * 0.5f)
            {
                inSound = false;
            }
            
            msOffset += 5;
            
            // 最多搜索前10秒，找够所需数量
            if (msOffset > 10000 || positions.Count >= VariantCount * 3) break;
        }
        
        return positions;
    }
    
    /// <summary>
    /// 从 MP3 文件中截取指定时长的片段并保存为 WAV，带淡入淡出
    /// </summary>
    public static void ExtractClipWithFade(string inputPath, string outputPath, int startMs, int durationMs, 
        float volumeAdjustDb = 0, int fadeInMs = 2, int fadeOutMs = 15)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input audio file not found", inputPath);

        using var reader = new AudioFileReader(inputPath);
        
        // 计算位置
        var totalMs = (int)(reader.TotalTime.TotalMilliseconds);
        startMs = Math.Min(startMs, totalMs - durationMs);
        if (startMs < 0) startMs = 0;
        
        // 设置起始位置
        reader.CurrentTime = TimeSpan.FromMilliseconds(startMs);
        
        // 计算音量系数
        var volumeFactor = (float)Math.Pow(10, volumeAdjustDb / 20.0);
        
        // 读取样本
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var samplesNeeded = (int)(sampleRate * channels * durationMs / 1000.0);
        var buffer = new float[samplesNeeded];
        var samplesRead = reader.Read(buffer, 0, samplesNeeded);
        
        // 计算淡入淡出的样本数
        var fadeInSamples = sampleRate * channels * fadeInMs / 1000;
        var fadeOutSamples = sampleRate * channels * fadeOutMs / 1000;
        
        // 应用音量调整和淡入淡出
        for (int i = 0; i < samplesRead; i++)
        {
            float fade = 1.0f;
            
            // 淡入
            if (i < fadeInSamples)
            {
                fade = (float)i / fadeInSamples;
            }
            // 淡出
            else if (i > samplesRead - fadeOutSamples)
            {
                fade = (float)(samplesRead - i) / fadeOutSamples;
            }
            
            buffer[i] *= volumeFactor * fade;
            
            // 限幅
            if (buffer[i] > 1.0f) buffer[i] = 1.0f;
            if (buffer[i] < -1.0f) buffer[i] = -1.0f;
        }
        
        // 创建输出目录
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);
        
        // 写入 WAV 文件
        var outFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(outputPath, outFormat);
        writer.WriteSamples(buffer, 0, samplesRead);
    }
    
    /// <summary>
    /// 确保打字机音效变体文件存在，如果不存在则从源文件截取多个变体
    /// 返回所有变体文件的路径列表
    /// </summary>
    public static List<string> EnsureTypewriterSoundVariants()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        Directory.CreateDirectory(assetsDir);
        
        var variants = new List<string>();
        
        // 检查是否所有变体都已存在
        bool allExist = true;
        for (int i = 0; i < VariantCount; i++)
        {
            var path = Path.Combine(assetsDir, $"typewriter_v{i}.wav");
            if (!File.Exists(path))
            {
                allExist = false;
                break;
            }
            variants.Add(path);
        }
        
        if (allExist)
            return variants;
        
        // 需要重新生成
        variants.Clear();
        
        // 查找源音频文件
        var sourcePath = Path.Combine(assetsDir, "typewriter-typing-68696.mp3");
        if (!File.Exists(sourcePath))
        {
            // 尝试项目目录
            var projectAssetsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets"));
            sourcePath = Path.Combine(projectAssetsDir, "typewriter-typing-68696.mp3");
        }
        
        // 如果源文件不存在，生成合成音效变体
        if (!File.Exists(sourcePath))
        {
            return GenerateSyntheticVariants(assetsDir);
        }
        
        try
        {
            // 找到所有按键声音位置
            var keyPresses = FindAllKeyPressesMs(sourcePath, 0.03f, 80);
            System.Diagnostics.Debug.WriteLine($"Found {keyPresses.Count} key presses in source audio");
            
            if (keyPresses.Count < VariantCount)
            {
                // 如果找到的按键不够，用合成音效补充
                return GenerateSyntheticVariants(assetsDir);
            }
            
            // 选择分散的按键位置，避免连续的声音太相似
            var selectedIndices = new List<int>();
            var step = keyPresses.Count / VariantCount;
            for (int i = 0; i < VariantCount; i++)
            {
                selectedIndices.Add(Math.Min(i * step, keyPresses.Count - 1));
            }
            
            // 从不同位置截取变体
            for (int i = 0; i < VariantCount; i++)
            {
                var outputPath = Path.Combine(assetsDir, $"typewriter_v{i}.wav");
                
                // 删除旧文件
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                
                var startMs = keyPresses[selectedIndices[i]];
                // 截取60ms，带淡出效果，音量降低3dB使其更柔和
                ExtractClipWithFade(sourcePath, outputPath, startMs, 60, -3f, 1, 20);
                
                variants.Add(outputPath);
                System.Diagnostics.Debug.WriteLine($"Extracted variant {i} from {startMs}ms");
            }
            
            return variants;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureTypewriterSoundVariants error: {ex.Message}");
            return GenerateSyntheticVariants(assetsDir);
        }
    }
    
    /// <summary>
    /// 生成多个合成音效变体（当源文件不可用时使用）
    /// </summary>
    private static List<string> GenerateSyntheticVariants(string assetsDir)
    {
        var variants = new List<string>();
        var rng = new Random(42); // 固定种子确保每次生成相同
        
        // 不同的基础频率，模拟不同按键的声音
        var baseFreqs = new[] { 800.0, 900.0, 1000.0, 1100.0, 950.0 };
        
        for (int v = 0; v < VariantCount; v++)
        {
            var outputPath = Path.Combine(assetsDir, $"typewriter_v{v}.wav");
            
            var sampleRate = 44100;
            var durationMs = 50 + rng.Next(20); // 50-70ms，略有不同
            var sampleCount = sampleRate * durationMs / 1000;
            var buffer = new float[sampleCount];
            
            var freq = baseFreqs[v] + rng.NextDouble() * 100 - 50; // 基频±50Hz变化
            var decayRate = 6.0 + rng.NextDouble() * 2; // 衰减速率略有不同
            
            for (int i = 0; i < sampleCount; i++)
            {
                var t = (double)i / sampleRate;
                var envelope = Math.Exp(-decayRate * t);
                
                // 混合多个谐波，更接近真实机械声
                var tone = Math.Sin(2 * Math.PI * freq * t) * 0.5
                         + Math.Sin(2 * Math.PI * freq * 2 * t) * 0.2
                         + Math.Sin(2 * Math.PI * freq * 0.5 * t) * 0.15;
                
                // 添加少量噪声
                var noise = (rng.NextDouble() - 0.5) * 0.1;
                
                // 低音量，柔和
                buffer[i] = (float)((tone + noise) * envelope * 0.08);
            }
            
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            using (var writer = new WaveFileWriter(outputPath, format))
            {
                writer.WriteSamples(buffer, 0, buffer.Length);
            }
            
            variants.Add(outputPath);
        }
        
        return variants;
    }
    
    /// <summary>
    /// 确保打字机音效文件存在（兼容旧API，返回第一个变体）
    /// </summary>
    public static string EnsureTypewriterSound()
    {
        var variants = EnsureTypewriterSoundVariants();
        return variants.Count > 0 ? variants[0] : string.Empty;
    }
}
