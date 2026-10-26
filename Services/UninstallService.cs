using System;
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
    /// <summary>
    /// Performs the full uninstall flow: removes binary, menu/shortcut,
    /// icon, marker file, and optionally user data.
    /// </summary>
    /// <param name="removeUserData">
    /// If true, also removes the config file and users database.
    /// </param>
    /// <returns>True if the uninstall started successfully.</returns>
    public static bool Uninstall(bool removeUserData = false)
    {
        var info = InstallationService.GetInstallInfo();
        if (info is null || string.IsNullOrEmpty(info.BinaryPath) || !File.Exists(info.BinaryPath))
        {
            // Nothing installed; still clean up marker if present
            // (this can happen if the binary was moved/removed manually)
            InstallationService.SaveInstallInfo(InstallScope.User, string.Empty);
            return false;
        }

        var scope = info.Scope;
        var paths = InstallationService.GetInstallPaths(scope);

        bool ok = true;

        // 1. Remove the binary
        try
        {
            if (File.Exists(paths.Binary))
                File.Delete(paths.Binary);
        }
        catch
        {
            ok = false;
        }

        // 2. Remove the menu entry / shortcut
        try
        {
            if (File.Exists(paths.MenuEntry))
                File.Delete(paths.MenuEntry);
        }
        catch
        {
            ok = false;
        }

        // 3. Remove the icon
        try
        {
            if (File.Exists(paths.Icon))
                File.Delete(paths.Icon);
        }
        catch
        {
            ok = false;
        }

        // 4. Remove the install marker file
        try
        {
            var markerPath = InstallationService.GetMarkerPath();
            if (File.Exists(markerPath))
                File.Delete(markerPath);
        }
        catch
        {
            ok = false;
        }

        // 5. Optionally remove user data
        if (removeUserData)
        {
            try
            {
                var configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "learn-assist");

                // Remove config.enc
                var configPath = Path.Combine(configDir, "config.enc");
                if (File.Exists(configPath))
                    File.Delete(configPath);

                // Remove users.json
                var usersPath = Path.Combine(configDir, "users.json");
                if (File.Exists(usersPath))
                    File.Delete(usersPath);

                // Remove the config directory if empty
                try { Directory.Delete(configDir, true); }
                catch { }
            }
            catch
            {
                ok = false;
            }
        }

        // Clear the in-memory install info
        InstallationService.SaveInstallInfo(InstallScope.User, string.Empty);

        return ok;
    }
}
