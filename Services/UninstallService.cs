using System;
using System.Diagnostics;
using System.IO;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Reverse of InstallationService: removes the installed app binary,
/// menu entry, icon, and marker file for both Windows and Linux.
/// Also moves the application data (config and accounts) to the system trash.
/// </summary>
public static class UninstallService
{
    private const string LogSource = nameof(UninstallService);

    /// <summary>
    /// Performs the full uninstall flow: removes binary, menu/shortcut,
    /// icon, marker file, and optionally user data.
    /// </summary>
    /// <param name="removeUserData">
    /// If true, also removes the config file and users database.
    /// </param>
    /// <returns>True if all removals succeeded.</returns>
    public static bool Uninstall(bool removeUserData = true)
    {
        var info = InstallationService.GetInstallInfo();
        if (info is null)
        {
            return false;
        }

        var scope = info.Scope;
        var paths = InstallationService.GetInstallPaths(scope);

        bool ok = true;

        // 1. Remove the binary
        try
        {
            if (File.Exists(paths.Binary))
            {
                // If we're trying to delete the running executable, we can't
                // do it directly on Windows. Elevate and let the worker handle it.
                if (string.Equals(paths.Binary, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (scope == InstallScope.System)
                    {
                        if (!InstallationService.ElevatedTrash(paths.Binary))
                            ok = false;
                    }
                    else
                    {
                        FileTrashService.ScheduleAfterProcessExit(paths.Binary);
                    }
                }
                else
                {
                    FileTrashService.MoveToTrash(paths.Binary);
                }
            }
        }
        catch (Exception ex)
        {
            LogUninstallError("delete binary", ex);
            ok = false;
        }

        // 2. Remove the menu entry / shortcut
        try
        {
            if (File.Exists(paths.MenuEntry))
            {
                if (scope == InstallScope.System && OperatingSystem.IsWindows())
                {
                    if (!InstallationService.ElevatedTrash(paths.MenuEntry))
                        ok = false;
                }
                else
                {
                    FileTrashService.MoveToTrash(paths.MenuEntry);
                }
            }
        }
        catch (Exception ex)
        {
            LogUninstallError("delete menu entry", ex);
            ok = false;
        }

        // 3. Remove the icon
        try
        {
            if (File.Exists(paths.Icon))
            {
                if (scope == InstallScope.System && OperatingSystem.IsWindows())
                {
                    if (!InstallationService.ElevatedTrash(paths.Icon))
                        ok = false;
                }
                else
                {
                    FileTrashService.MoveToTrash(paths.Icon);
                }
            }
        }
        catch (Exception ex)
        {
            LogUninstallError("delete icon", ex);
            ok = false;
        }

        // 4. Move all application data, including the marker, database,
        // encrypted configuration, profiles, documents and sessions.
        if (removeUserData)
        {
            try
            {
                var configDir = InstallationService.GetConfigDir();
                FileTrashService.ScheduleAfterProcessExit(configDir, directory: true);
            }
            catch (Exception ex)
            {
                LogUninstallError("move user data to trash", ex);
                ok = false;
            }
        }

        return ok;
    }

    private static void LogUninstallError(string step, Exception ex)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "learn-assist");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "startup-error.log"),
                $"{DateTime.UtcNow:O} [{LogSource}] Uninstall failed at '{step}': {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must not prevent the app from continuing.
        }
    }
}