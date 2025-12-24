using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MyBook.Services;

/// <summary>
/// 视觉小说发布服务 - 使用 dotnet publish 将当前故事打包为独立可运行的程序
/// </summary>
public class PublishService
{
    /// <summary>
    /// 发布进度报告
    /// </summary>
    public event Action<string, int>? ProgressChanged;

    /// <summary>
    /// 获取当前运行时标识符
    /// </summary>
    private static string GetCurrentRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => "win-x64"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => "linux-x64"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => "osx-x64"
            };
        }
        return "win-x64";
    }

    /// <summary>
    /// 发布视觉小说到指定目录
    /// </summary>
    /// <param name="targetDirectory">目标目录</param>
    /// <param name="novelName">小说名称（用于可执行文件名）</param>
    /// <param name="runtimeIdentifier">目标运行时（如 win-x64, linux-x64, osx-arm64）</param>
    public async Task PublishAsync(string targetDirectory, string novelName, string? runtimeIdentifier = null)
    {
        // 清理小说名称，移除非法字符
        novelName = CleanFileName(novelName);
        if (string.IsNullOrWhiteSpace(novelName))
            novelName = "MyNovel";

        runtimeIdentifier ??= GetCurrentRuntimeIdentifier();

        // 查找项目文件
        var projectDir = FindProjectDirectory();
        if (string.IsNullOrEmpty(projectDir))
        {
            throw new Exception("无法找到项目文件 (.csproj)。请确保从开发环境运行发布功能。");
        }

        var projectFile = Path.Combine(projectDir, "MyBook.csproj");
        if (!File.Exists(projectFile))
        {
            throw new Exception($"项目文件不存在: {projectFile}");
        }

        try
        {
            // 1. 创建临时发布配置
            ReportProgress("准备发布配置...", 5);
            
            // 创建目标目录
            Directory.CreateDirectory(targetDirectory);

            // 2. 执行 dotnet publish
            ReportProgress("正在编译和发布程序（这可能需要几分钟）...", 10);
            
            var publishArgs = $"publish \"{projectFile}\" " +
                            $"-c Release " +
                            $"-r {runtimeIdentifier} " +
                            $"--self-contained true " +
                            $"-o \"{targetDirectory}\" " +
                            $"-p:PublishSingleFile=false " +  // 不使用单文件，因为需要Assets
                            $"-p:IncludeNativeLibrariesForSelfExtract=true " +
                            $"-p:EnableCompressionInSingleFile=true";

            var result = await RunDotNetCommandAsync(publishArgs);
            
            if (!result.Success)
            {
                throw new Exception($"发布失败:\n{result.Error}");
            }

            ReportProgress("复制资源文件...", 70);

            // 3. 复制 Assets 和 Stories 文件夹（确保包含最新数据）
            var baseDir = AppContext.BaseDirectory;
            
            // 复制 Assets
            var sourceAssets = Path.Combine(baseDir, "Assets");
            var targetAssets = Path.Combine(targetDirectory, "Assets");
            if (Directory.Exists(sourceAssets))
            {
                await CopyDirectoryAsync(sourceAssets, targetAssets);
            }

            // 复制 Stories
            var sourceStories = Path.Combine(baseDir, "Stories");
            var targetStories = Path.Combine(targetDirectory, "Stories");
            if (Directory.Exists(sourceStories))
            {
                await CopyDirectoryAsync(sourceStories, targetStories);
            }

            // 4. 创建发布配置文件（标记为发布版本，不包含编辑器）
            ReportProgress("创建启动配置...", 85);
            await CreatePublishConfigAsync(targetDirectory, novelName);

            // 5. 重命名可执行文件
            ReportProgress("完成发布...", 95);
            RenameExecutable(targetDirectory, novelName, runtimeIdentifier);

            // 6. 清理不需要的文件
            CleanupPublishDirectory(targetDirectory);

            ReportProgress("发布完成！", 100);
        }
        catch (Exception ex)
        {
            throw new Exception($"发布失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 查找项目目录
    /// </summary>
    private string? FindProjectDirectory()
    {
        // 从当前运行目录向上查找 .csproj 文件
        var currentDir = AppContext.BaseDirectory;
        
        // 尝试常见的项目结构
        var possiblePaths = new[]
        {
            currentDir,
            Path.Combine(currentDir, ".."),
            Path.Combine(currentDir, "..", ".."),
            Path.Combine(currentDir, "..", "..", ".."),
            Path.Combine(currentDir, "..", "..", "..", ".."),
            Path.Combine(currentDir, "..", "..", "..", "..", ".."),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                var csprojFiles = Directory.GetFiles(fullPath, "*.csproj");
                if (csprojFiles.Length > 0)
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 执行 dotnet 命令
    /// </summary>
    private async Task<(bool Success, string Output, string Error)> RunDotNetCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        
        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                // 解析进度
                if (e.Data.Contains("Compiling"))
                    ReportProgress("正在编译...", 30);
                else if (e.Data.Contains("Optimizing"))
                    ReportProgress("正在优化...", 50);
                else if (e.Data.Contains("Generating"))
                    ReportProgress("正在生成...", 60);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
                error.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode == 0, output.ToString(), error.ToString());
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        await Task.Run(() =>
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetPath = Path.Combine(targetDir, fileName);
                File.Copy(file, targetPath, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectoryAsync(dir, Path.Combine(targetDir, dirName)).Wait();
            }
        });
    }

    /// <summary>
    /// 创建发布配置文件
    /// </summary>
    private async Task CreatePublishConfigAsync(string targetDir, string novelName)
    {
        var configPath = Path.Combine(targetDir, "publish.config");
        var config = new Dictionary<string, string>
        {
            ["NovelName"] = novelName,
            ["PublishDate"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["IncludeEditor"] = "False",  // 发布版本不包含编辑器
            ["IsPublishedVersion"] = "true"
        };

        var configContent = string.Join(Environment.NewLine,
            config.Select(kv => $"{kv.Key}={kv.Value}"));

        await File.WriteAllTextAsync(configPath, configContent);
    }

    /// <summary>
    /// 重命名可执行文件
    /// </summary>
    private void RenameExecutable(string targetDir, string novelName, string runtimeId)
    {
        string exeExtension = runtimeId.StartsWith("win") ? ".exe" : "";
        var originalExe = Path.Combine(targetDir, $"MyBook{exeExtension}");
        var newExe = Path.Combine(targetDir, $"{novelName}{exeExtension}");

        if (File.Exists(originalExe) && originalExe != newExe)
        {
            if (File.Exists(newExe))
                File.Delete(newExe);
            File.Copy(originalExe, newExe);
        }
    }

    /// <summary>
    /// 清理发布目录中不需要的文件
    /// </summary>
    private void CleanupPublishDirectory(string targetDir)
    {
        // 删除 .pdb 文件（调试符号）
        foreach (var pdb in Directory.GetFiles(targetDir, "*.pdb"))
        {
            try { File.Delete(pdb); } catch { }
        }

        // 删除开发相关文件
        var filesToDelete = new[] { "*.Development.json", "appsettings.Development.json" };
        foreach (var pattern in filesToDelete)
        {
            foreach (var file in Directory.GetFiles(targetDir, pattern))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private string CleanFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
    }

    /// <summary>
    /// 报告进度
    /// </summary>
    private void ReportProgress(string message, int percentage)
    {
        ProgressChanged?.Invoke(message, percentage);
    }

    /// <summary>
    /// 获取发布所需的预估大小（MB）
    /// </summary>
    public async Task<double> EstimatePublishSizeAsync()
    {
        // 自包含发布通常在 60-150MB 之间
        // 加上 Assets 和 Stories
        var baseDir = AppContext.BaseDirectory;
        double assetsSize = 0;

        await Task.Run(() =>
        {
            var assetsDir = Path.Combine(baseDir, "Assets");
            if (Directory.Exists(assetsDir))
                assetsSize += GetDirectorySize(assetsDir);

            var storiesDir = Path.Combine(baseDir, "Stories");
            if (Directory.Exists(storiesDir))
                assetsSize += GetDirectorySize(storiesDir);
        });

        // 估算：运行时约80MB + 资源
        return 80 + (assetsSize / (1024 * 1024));
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try { size += new FileInfo(file).Length; } catch { }
        }
        return size;
    }

    /// <summary>
    /// 检查是否为已发布版本
    /// </summary>
    public static bool IsPublishedVersion()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "publish.config");
        if (!File.Exists(configPath))
            return false;

        try
        {
            var content = File.ReadAllText(configPath);
            return content.Contains("IsPublishedVersion=true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取发布配置
    /// </summary>
    public static Dictionary<string, string> GetPublishConfig()
    {
        var config = new Dictionary<string, string>();
        var configPath = Path.Combine(AppContext.BaseDirectory, "publish.config");

        if (!File.Exists(configPath))
            return config;

        try
        {
            var lines = File.ReadAllLines(configPath);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    config[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }
        catch { }

        return config;
    }

    /// <summary>
    /// 获取支持的运行时标识符列表
    /// </summary>
    public static List<(string Id, string DisplayName)> GetSupportedRuntimeIdentifiers()
    {
        return new List<(string, string)>
        {
            ("win-x64", "Windows 64位"),
            ("win-x86", "Windows 32位"),
            ("win-arm64", "Windows ARM64"),
            ("linux-x64", "Linux 64位"),
            ("linux-arm64", "Linux ARM64"),
            ("osx-x64", "macOS Intel"),
            ("osx-arm64", "macOS Apple Silicon")
        };
    }
}
