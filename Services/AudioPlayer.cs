using System;
using System.IO;
using NAudio.Wave;

namespace MyBook.Services
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? _output;
        private AudioFileReader? _reader;

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
                _reader = new AudioFileReader(resolved);
                _output = new WaveOutEvent();
                _output.Init(_reader);
                _output.Play();
                WriteLog($"Playing audio: {resolved}");
            }
            catch (Exception ex)
            {
                WriteLog($"Audio playback error for {resolved}: {ex.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            try
            {
                _output?.Stop();
                _output?.Dispose();
                _output = null;
            }
            catch { }

            try
            {
                _reader?.Dispose();
                _reader = null;
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
