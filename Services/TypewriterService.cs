using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace MyBook.Services;

/// <summary>
/// 打字机效果服务 - 逐字显示文本并播放打字音效
/// 使用多个音效变体和随机化实现自然的打字声音
/// </summary>
public class TypewriterService
{
    private readonly AudioPlayer _audioPlayer;
    private readonly object _soundInitLock = new();
    private readonly Random _rng = new();
    private List<string> _soundVariants = new();
    private CancellationTokenSource? _cts;
    private bool _isTyping;
    private int _lastVariantIndex = -1;
    
    // 打字速度（毫秒/字符）
    public int CharacterDelay { get; set; } = 35;
    
    // 是否启用打字音效
    public bool EnableSound { get; set; } = true;
    public float TypeSoundVolume { get; set; } = 0.4f;
    
    // 每隔几个字符播放一次音效（避免太吵）
    public int SoundInterval { get; set; } = 2;
    
    // 音量随机范围（±百分比）
    public float VolumeVariation { get; set; } = 0.15f;
    
    // 当前是否正在打字
    public bool IsTyping => _isTyping;
    
    // 完整文本
    private string _fullText = string.Empty;
    public string FullText => _fullText;

    public TypewriterService()
    {
        _audioPlayer = new AudioPlayer();
        // 异步初始化音效变体，避免阻塞构造函数
        Task.Run(InitializeSoundVariants);
    }
    
    /// <summary>
    /// 初始化音效变体
    /// </summary>
    private void InitializeSoundVariants()
    {
        lock (_soundInitLock)
        {
            try
            {
                _soundVariants = AudioClipExtractor.EnsureTypewriterSoundVariants();
                System.Diagnostics.Debug.WriteLine($"Loaded {_soundVariants.Count} typewriter sound variants");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeSoundVariants error: {ex.Message}");
                _soundVariants = new List<string>();
            }
        }
    }

    /// <summary>
    /// 开始打字机效果
    /// </summary>
    /// <param name="text">要显示的完整文本</param>
    /// <param name="onTextUpdate">每次更新文本时的回调</param>
    /// <param name="onComplete">打字完成时的回调</param>
    public async Task StartTypingAsync(string text, Action<string> onTextUpdate, Action? onComplete = null)
    {
        // 取消之前的打字
        Stop();
        
        _fullText = text;
        _cts = new CancellationTokenSource();
        _isTyping = true;
        
        var displayedText = string.Empty;
        var charCount = 0;
        
        try
        {
            foreach (var c in text)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;
                
                displayedText += c;
                onTextUpdate(displayedText);
                
                // 播放打字音效
                if (EnableSound && charCount % SoundInterval == 0 && !char.IsWhiteSpace(c))
                {
                    PlayTypeSound();
                }
                
                charCount++;
                
                // 根据字符类型调整延迟
                var delay = CharacterDelay;
                if (c == '。' || c == '！' || c == '？' || c == '.' || c == '!' || c == '?')
                {
                    delay = CharacterDelay * 6; // 句末停顿更长
                }
                else if (c == '，' || c == '、' || c == ',' || c == ';' || c == '；')
                {
                    delay = CharacterDelay * 3; // 逗号稍停
                }
                else if (c == '\n')
                {
                    delay = CharacterDelay * 4; // 换行稍停
                }
                
                await Task.Delay(delay, _cts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // 被取消，显示完整文本
            onTextUpdate(_fullText);
        }
        finally
        {
            _isTyping = false;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 立即完成打字，显示完整文本
    /// </summary>
    public void Complete()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// 停止打字
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isTyping = false;
    }

    private void PlayTypeSound()
    {
        try
        {
            // 确保音效变体已加载
            if (_soundVariants.Count == 0)
            {
                lock (_soundInitLock)
                {
                    if (_soundVariants.Count == 0)
                    {
                        _soundVariants = AudioClipExtractor.EnsureTypewriterSoundVariants();
                    }
                }
            }

            if (_soundVariants.Count == 0)
                return;
            
            // 随机选择一个变体（避免连续选择相同的）
            int variantIndex;
            if (_soundVariants.Count > 1)
            {
                do
                {
                    variantIndex = _rng.Next(_soundVariants.Count);
                } while (variantIndex == _lastVariantIndex && _soundVariants.Count > 2);
                _lastVariantIndex = variantIndex;
            }
            else
            {
                variantIndex = 0;
            }
            
            var soundPath = _soundVariants[variantIndex];
            
            if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
            {
                // 添加音量随机化（使声音更自然）
                var volumeMultiplier = 1.0f + ((float)_rng.NextDouble() * 2 - 1) * VolumeVariation;
                var finalVolume = TypeSoundVolume * volumeMultiplier;
                finalVolume = Math.Clamp(finalVolume, 0.05f, 1.0f);
                
                // 播放音效
                _audioPlayer.PlayOnce(soundPath, finalVolume);
            }
        }
        catch (Exception ex)
        {
            // 记录错误但不中断
            System.Diagnostics.Debug.WriteLine($"PlayTypeSound error: {ex.Message}");
        }
    }
}
