using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;
using learn_Assist.Models;

namespace learn_Assist.Services;

public sealed record InstallPaths(string Binary, string MenuEntry, string Icon);

/// <summary>
/// Performs a real system installation of the running binary (Linux: .desktop
/// menu entry, Windows: Start Menu shortcut) and tracks the state in a marker
/// file under <c>~/.config/learn-assist/install.json</c>.
/// System scope only elevates the privileged file copy (pkexec / UAC); the
/// menu entry and icon are always written to user locations.
/// </summary>
public static class InstallationService
{
    private const string MarkerFileName = "install.json";

    public static bool IsInstalled()
    {
        var info = GetInstallInfo();
        return info is not null
            && !string.IsNullOrEmpty(info.BinaryPath)
            && File.Exists(info.BinaryPath);
    }

    public static InstallInfo? GetInstallInfo()
    {
        var path = GetMarkerPath();
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<InstallInfo>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static InstallPaths GetInstallPaths(InstallScope scope)
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsPaths(scope);

        return GetLinuxPaths(scope);
    }

    /// <summary>
    /// Copies the running binary to its target location (elevating when needed)
    /// and creates the desktop menu entry + icon. Returns true on success.
    /// </summary>
    public static bool Install(InstallScope scope)
    {
        var self = Environment.ProcessPath;
        if (string.IsNullOrEmpty(self) || !File.Exists(self))
            return false;

        var paths = GetInstallPaths(scope);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Binary)!);

            if (IsSystemScope(scope))
            {
                if (!ElevatedCopy(self, paths.Binary))
                    return false;
            }
            else
            {
                File.Copy(self, paths.Binary, overwrite: true);
            }

            WriteIcon(paths.Icon);
            WriteMenuEntry(scope, paths);
            SaveInstallInfo(scope, paths.Binary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSystemScope(InstallScope scope) =>
        scope == InstallScope.System && (OperatingSystem.IsLinux() || OperatingSystem.IsWindows());

    private static InstallPaths GetLinuxPaths(InstallScope scope)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(userData))
            userData = Path.Combine(home, ".local", "share");

        string binary;
        if (scope == InstallScope.System)
            binary = "/opt/learn-assist/learn-assist";
        else
            binary = Path.Combine(userData, "learn-assist", "learn-assist");

        var menuEntry = Path.Combine(userData, "applications", "learn-assist.desktop");
        var icon = Path.Combine(userData, "icons", "hicolor", "256x256", "apps", "learn-assist.png");
        return new InstallPaths(binary, menuEntry, icon);
    }

    private static InstallPaths GetWindowsPaths(InstallScope scope)
    {
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs");
        var menuEntry = Path.Combine(startMenu, "LearnAssist.lnk");

        if (scope == InstallScope.System)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return new InstallPaths(
                Path.Combine(programFiles, "LearnAssist", "learn-assist.exe"),
                menuEntry,
                Path.Combine(programFiles, "LearnAssist", "learn-assist.png"));
        }

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var icon = Path.Combine(localData, "learn-assist", "learn-assist.png");
        return new InstallPaths(
            Path.Combine(localData, "learn-assist", "learn-assist.exe"),
            menuEntry,
            icon);
    }

    private static bool ElevatedCopy(string source, string target)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ElevatedCopyWindows(source, target);

            var psi = new ProcessStartInfo
            {
                FileName = "pkexec",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("install");
            psi.ArgumentList.Add("-D");
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("755");
            psi.ArgumentList.Add(source);
            psi.ArgumentList.Add(target);

            using var process = Process.Start(psi);
            if (process is null)
                return false;
            return process.WaitForExit(120_000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunUacWindows(string source, string target)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(Environment.ProcessPath!),
        };
        psi.ArgumentList.Add("--install-elevated");
        psi.ArgumentList.Add(source);
        psi.ArgumentList.Add(target);

        using var process = Process.Start(psi);
        if (process is null)
            return false; // user cancelled the UAC prompt
        if (!process.WaitForExit(120_000))
        {
            process.Kill();
            return false;
        }

        return process.ExitCode == 0 && File.Exists(target);
    }

    private static bool ElevatedCopyWindows(string source, string target)
        => RunUacWindows(source, target);

    private static void WriteIcon(string iconPath)
    {
        var dir = Path.GetDirectoryName(iconPath);
        if (string.IsNullOrEmpty(dir))
            return;
        Directory.CreateDirectory(dir);

        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var uri = new Uri($"avares://{assemblyName}/Assets/avalonia-logo.png");
        using var stream = AssetLoader.Open(uri);
        using var file = File.Create(iconPath);
        stream.CopyTo(file);
    }

    private static void WriteMenuEntry(InstallScope scope, InstallPaths paths)
    {
        var dir = Path.GetDirectoryName(paths.MenuEntry);
        if (string.IsNullOrEmpty(dir))
            return;
        Directory.CreateDirectory(dir);

        if (OperatingSystem.IsWindows())
        {
            WriteWindowsShortcut(paths);
            return;
        }

        var desktop = string.Join('\n', new[]
        {
            "[Desktop Entry]",
            "Type=Application",
            "Version=1.0",
            "Name=learn-Assist",
            "Comment=AI-powered learning assistant",
            $"Exec={paths.Binary}",
            $"Icon={paths.Icon}",
            "Terminal=false",
            "Categories=Education;Utility;",
            "StartupNotify=true",
        });
        File.WriteAllText(paths.MenuEntry, desktop + "\n");
    }

    private static void WriteWindowsShortcut(InstallPaths paths)
    {
        var binaryDir = Path.GetDirectoryName(paths.Binary);
        var argument = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{0}');" +
            " $s.TargetPath='{1}'; $s.WorkingDirectory='{2}'; $s.Save()";
        var script = string.Format(argument, paths.MenuEntry.Replace("'", "''"), paths.Binary, binaryDir);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-STA");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        using var process = Process.Start(psi);
        process?.WaitForExit();
    }

    private static string GetConfigDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "learn-assist");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetMarkerPath() => Path.Combine(GetConfigDir(), MarkerFileName);

    private static void SaveInstallInfo(InstallScope scope, string binaryPath)
    {
        var info = new InstallInfo
        {
            Scope = scope,
            BinaryPath = binaryPath,
            InstallDate = DateTime.Now,
        };
        File.WriteAllText(GetMarkerPath(), JsonSerializer.Serialize(info));
    }
}