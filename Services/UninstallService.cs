using System;
using System.Diagnostics;
using System.IO;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Reverse of InstallationService: removes the installed app binary,
/// menu entry, icon, and marker file for both Windows and Linux.
/// Also optionally removes user data (config and accounts).
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
    public static bool Uninstall(bool removeUserData = false)
    {
        var info = InstallationService.GetInstallInfo();
        if (info is null || string.IsNullOrEmpty(info.BinaryPath) || !File.Exists(info.BinaryPath))
        {
            // Nothing installed; clean up orphan marker if present
            try
            {
                var markerPath = InstallationService.GetMarkerPath();
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            catch (Exception ex)
            {
                LogUninstallError("cleanup orphan marker", ex);
            }
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
                        if (!InstallationService.ElevatedDelete(paths.Binary))
                            ok = false;
                    }
                    else
                    {
                        // User-scope but still running: cannot delete while in use.
                        LogUninstallError("delete running binary", new InvalidOperationException(
                            $"Cannot uninstall while the application is running from {paths.Binary}"));
                        ok = false;
                    }
                }
                else
                {
                    File.Delete(paths.Binary);
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
                    if (!InstallationService.ElevatedDelete(paths.MenuEntry))
                        ok = false;
                }
                else
                {
                    File.Delete(paths.MenuEntry);
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
                    if (!InstallationService.ElevatedDelete(paths.Icon))
                        ok = false;
                }
                else
                {
                    File.Delete(paths.Icon);
                }
            }
        }
        catch (Exception ex)
        {
            LogUninstallError("delete icon", ex);
            ok = false;
        }

        // 4. Remove the install marker file — only after successful deletion of everything else
        try
        {
            var markerPath = InstallationService.GetMarkerPath();
            if (File.Exists(markerPath))
                File.Delete(markerPath);
        }
        catch (Exception ex)
        {
            LogUninstallError("delete marker file", ex);
            ok = false;
        }

        // 5. Optionally remove user data
        if (removeUserData && ok)
        {
            try
            {
                var configDir = InstallationService.GetConfigDir();

                // Remove config.enc
                var configPath = Path.Combine(configDir, "config.enc");
                if (File.Exists(configPath))
                    File.Delete(configPath);

                // Remove users.json
                var usersPath = Path.Combine(configDir, "users.json");
                if (File.Exists(usersPath))
                    File.Delete(usersPath);
            }
            catch (Exception ex)
            {
                LogUninstallError("delete user data", ex);
                ok = false;
            }
        }

        // Clear the install info marker only if uninstall fully succeeded
        if (ok)
        {
            try
            {
                InstallationService.SaveInstallInfo(InstallScope.User, string.Empty);
            }
            catch (Exception ex)
            {
                LogUninstallError("clear install info", ex);
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