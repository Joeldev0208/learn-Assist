using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace learn_Assist.Services;

public static class LanguageService
{
    private const string FileName = "language.json";
    private static string? _current;

    public static string Current => _current ??= Load();

    public static bool IsEnglish =>
        string.Equals(Current, "English", StringComparison.OrdinalIgnoreCase);

    public static void Set(string language)
    {
        _current = string.Equals(language, "English", StringComparison.OrdinalIgnoreCase)
            ? "English"
            : "Español";
        Save(_current);
    }

    private static string Load()
    {
        try
        {
            var path = GetPath();
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
                if (saved is "English" or "Español")
                    return saved;
            }
        }
        catch
        {
        }

        return string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "en",
            StringComparison.OrdinalIgnoreCase)
            ? "English"
            : "Español";
    }

    private static void Save(string language)
    {
        try
        {
            var directory = GetConfigDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(GetPath(), JsonSerializer.Serialize(language));
        }
        catch
        {
        }
    }

    private static string GetConfigDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "learn-assist");

    private static string GetPath() => Path.Combine(GetConfigDirectory(), FileName);
}
