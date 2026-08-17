using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;

namespace learn_Assist.Services;

/// <summary>
/// Persists the user's light/dark theme choice to
/// <c>~/.config/learn-assist/theme.json</c> and applies it to the application.
/// The variant follows the system default until the user toggles it once.
/// </summary>
public static class ThemeService
{
    private const string FileName = "theme.json";
    private static string? _savedVariant;

    public static event Action? ThemeChanged;

    public static ThemeVariant Current =>
        Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;

    public static bool IsDark =>
        Current == ThemeVariant.Dark;

    public static void Apply()
    {
        var variant = Load();
        if (variant is not null && Application.Current is not null)
            Application.Current.RequestedThemeVariant = variant;
    }

    public static void Toggle()
    {
        Set(IsDark ? ThemeVariant.Light : ThemeVariant.Dark);
    }

    public static void Set(ThemeVariant variant)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = variant;
        Save(variant);
        ThemeChanged?.Invoke();
    }

    private static string GetConfigDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "learn-assist");
    }

    private static string GetPath() => Path.Combine(GetConfigDir(), FileName);

    private static ThemeVariant? Load()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            _savedVariant = JsonSerializer.Deserialize<string>(json);
            return _savedVariant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        catch
        {
            return null;
        }
    }

    private static void Save(ThemeVariant variant)
    {
        try
        {
            var dir = GetConfigDir();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(GetPath(), JsonSerializer.Serialize(variant.Key?.ToString() ?? "Light"));
        }
        catch
        {
        }
    }
}