using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace learn_Assist.Services;

public static class FileTrashService
{
    public static void MoveToTrash(string path)
    {
        if (!File.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        MoveWithDesktopTrashTool(path, "archivo");
    }

    public static void MoveDirectoryToTrash(string path)
    {
        if (!Directory.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
        {
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        MoveWithDesktopTrashTool(path, "carpeta");
    }

    public static void ScheduleAfterProcessExit(string path, bool directory = false)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("No se pudo localizar el ejecutable para completar la desinstalación.");

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--trash-after-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(directory ? "directory" : "file");
        startInfo.ArgumentList.Add(path);
        Process.Start(startInfo)?.Dispose();
    }

    private static void MoveWithDesktopTrashTool(string path, string kind)
    {
        var tool = FindTrashTool();
        if (tool is null)
            throw new PlatformNotSupportedException(
                $"No se encontró una herramienta del sistema para enviar {kind} a la papelera.");

        var startInfo = new ProcessStartInfo
        {
            FileName = tool,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (tool == "gio")
            startInfo.ArgumentList.Add("trash");
        startInfo.ArgumentList.Add(path);

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
        if (process is null || process.ExitCode != 0)
            throw new IOException($"No se pudo enviar el {kind} a la papelera.");
    }

    private static string? FindTrashTool()
    {
        foreach (var tool in new[] { "gio", "trash-put" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tool,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                if (process is not null && process.WaitForExit(3000))
                    return tool;
            }
            catch
            {
                // Try the next standard desktop trash utility.
            }
        }

        return null;
    }
}
