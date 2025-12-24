using System;
using System.IO;
using NAudio.Wave;

namespace MyBook.Services
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? _output;
        private AudioFileReader? _reader;
        private string? _currentPath;
        
        /// <summary>
        /// 是否启用循环播放（BGM 默认启用）
        /// </summary>
        public bool Loop { get; set; } = true;

        public void Play(string path)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(path)) return;

            string resolved = path;
            if (!Path.IsPathRooted(path))
            {
                resolved = Path.Combine(AppContext.BaseDirectory, path);
            }

            if (!File.Exists(resolved))
            {
                WriteLog($"Audio file not found: {resolved}");
                return;
            }

            try
            {
                _currentPath = resolved;
                _reader = new AudioFileReader(resolved);
                _output = new WaveOutEvent();
                _output.Init(_reader);
                
                // 监听播放结束事件，实现循环播放
                _output.PlaybackStopped += OnPlaybackStopped;
                
                _output.Play();
                WriteLog($"Playing audio: {resolved} (Loop: {Loop})");
            }
            catch (Exception ex)
            {
                WriteLog($"Audio playback error for {resolved}: {ex.Message}");
                Stop();
            }
        }
        
        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // 如果启用循环且没有错误，则重新播放
            if (Loop && e.Exception == null && _reader != null && _output != null)
            {
                try
                {
                    // 重置到音频开头
                    _reader.Position = 0;
                    _output.Play();
                    WriteLog($"Looping audio: {_currentPath}");
                }
                catch (Exception ex)
                {
                    WriteLog($"Loop playback error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            try
            {
                if (_output != null)
                {
                    // 先移除事件监听，避免循环触发
                    _output.PlaybackStopped -= OnPlaybackStopped;
                    _output.Stop();
                    _output.Dispose();
                    _output = null;
                }
            }
            catch { }

            try
            {
                _reader?.Dispose();
                _reader = null;
            }
            catch { }
            
            _currentPath = null;
        }

        /// <summary>
        /// 播放一次性音效（不会中断当前背景音乐）
        /// </summary>
        /// <param name="volume">音量 0-1，默认 1</param>
        public void PlayOnce(string path, float volume = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string resolved = path;
            if (!Path.IsPathRooted(path))
            {
                resolved = Path.Combine(AppContext.BaseDirectory, path);
            }

            if (!File.Exists(resolved)) return;

            try
            {
                // 使用单独的播放器播放音效
                var reader = new AudioFileReader(resolved)
                {
                    Volume = Math.Clamp(volume, 0f, 1f)
                };
                var output = new WaveOutEvent();
                output.Init(reader);
                output.PlaybackStopped += (s, e) =>
                {
                    output.Dispose();
                    reader.Dispose();
                };
                output.Play();
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
        }

        private void WriteLog(string message)
        {
            try
            {
                var logFile = Path.Combine(AppContext.BaseDirectory, "audio_player.log");
                var line = $"{DateTime.Now:o} {message}{Environment.NewLine}";
                File.AppendAllText(logFile, line);
            }
            catch { }
        }
    }
}
