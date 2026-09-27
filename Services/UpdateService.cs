using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Self-updater for the installed app (Windows, user scope only). Checks the
/// GitHub releases/<c>latest</c> for a tag newer than the running version,
/// downloads the win-x64 zip, stages the new exe next to the installed one,
/// and hands off to a small <c>.cmd</c> script that swaps the binary once the
/// running process exits and relaunches it.
/// <para/>
/// Only active when the app is actually installed (install marker present),
/// running on Windows, and installed in user scope — system-scope installs
/// would need elevation to replace the binary.
/// </summary>
public sealed class UpdateService
{
    private const string Repo = "Joeldev0208/learn-Assist";
    private const string AssetName = "learn-assist-win-x64.zip";
    private const string StagedFileName = "learn-assist.new.exe";

    private readonly HttpClient _http;
    private readonly Version? _currentVersion;
    private readonly string? _installDir;
    private readonly bool _isSupported;
    private string? _downloadUrl;

    public bool IsSupported => _isSupported;

    public UpdateService()
    {
        _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;

        var info = InstallationService.GetInstallInfo();
        _installDir = info is null ? null : Path.GetDirectoryName(info.BinaryPath);

        _isSupported = OperatingSystem.IsWindows()
            && _currentVersion is { Major: > 0 }
            && info is not null
            && info.Scope != InstallScope.System
            && !string.IsNullOrEmpty(info.BinaryPath)
            && File.Exists(info.BinaryPath);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("learn-Assist");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>
    /// Returns the newest release when it is newer than the running version;
    /// <c>null</c> when unsupported, up-to-date, or the check failed.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_isSupported || _currentVersion is null)
            return null;

        UpdateInfo? update = null;
        try
        {
            using var response = await _http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (!TryParseVersion(tag, out var latest) || latest <= _currentVersion)
                return null;

            string? assetUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var name)
                        && string.Equals(name.GetString(), AssetName, StringComparison.OrdinalIgnoreCase)
                        && asset.TryGetProperty("browser_download_url", out var url))
                    {
                        assetUrl = url.GetString();
                        break;
                    }
                }
            }

            if (assetUrl is null)
                return null;

            _downloadUrl = assetUrl;
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
            update = new UpdateInfo
            {
                IsUpdateAvailable = true,
                LatestVersion = latest,
                ReleaseNotes = notes ?? string.Empty,
            };
        }
        catch
        {
            // Updates must never break the app — fail silently.
        }

        return update;
    }

    /// <summary>
    /// Downloads the release, extracts the win-x64 exe as <c>learn-assist.new.exe</c>
    /// next to the installed binary, and writes the apply/restart script.
    /// </summary>
    public async Task StageUpdateAsync()
    {
        if (!_isSupported || _installDir is null)
            throw new InvalidOperationException("Automatic updates are not available in this build.");
        if (string.IsNullOrEmpty(_downloadUrl))
            throw new InvalidOperationException("No update is staged to install.");

        var info = InstallationService.GetInstallInfo()
            ?? throw new InvalidOperationException("Could not locate the installed app.");
        if (info.Scope == InstallScope.System)
            throw new InvalidOperationException("System-scope installs must be updated manually with administrator rights.");

        var appPath = info.BinaryPath;
        if (string.IsNullOrEmpty(appPath) || Path.GetFileName(appPath).Length == 0)
            throw new InvalidOperationException("Could not resolve the installed binary path.");

        var staged = Path.Combine(_installDir, StagedFileName);
        var zipPath = Path.Combine(_installDir, "update.zip");

        try
        {
            using (var response = await _http.GetAsync(_downloadUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await response.Content.CopyToAsync(fs);
            }

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.Entries.FirstOrDefault(e =>
                    string.Equals(e.Name, "learn-assist-win-x64.exe", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("The update package does not contain the application executable.");
                if (File.Exists(staged))
                    File.Delete(staged);
                entry.ExtractToFile(staged, overwrite: true);
            }

            File.Delete(zipPath);
            WriteUpdaterScript(appPath, staged);
        }
        catch
        {
            TryDelete(zipPath);
            TryDelete(staged);
            throw;
        }
    }

    private void WriteUpdaterScript(string appPath, string staged)
    {
        var appName = Path.GetFileName(appPath);
        var scriptPath = Path.Combine(_installDir!, "apply-update.cmd");

        var script = string.Join("\r\n", new[]
        {
            "@echo off",
            "setlocal",
            $"set \"APP={appPath}\"",
            $"set \"NEW={staged}\"",
            ":wait",
            $"tasklist /FI \"IMAGENAME eq {appName}\" 2>nul | find /I \"{appName}\" >nul",
            "if %errorlevel%==0 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto wait",
            ")",
            "move /Y \"%NEW%\" \"%APP%\" >nul",
            "start \"\" \"%APP%\"",
            "del \"%~f0\"",
            "endlocal",
        });

        File.WriteAllText(scriptPath, script);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _installDir,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add($"\"{scriptPath}\"");
        System.Diagnostics.Process.Start(psi);
    }

    private static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version();
        if (string.IsNullOrEmpty(tag))
            return false;
        var cleaned = tag.TrimStart('v', 'V');
        return Version.TryParse(cleaned, out version);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}