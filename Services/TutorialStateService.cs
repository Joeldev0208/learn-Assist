using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace learn_Assist.Services;

public static class TutorialStateService
{
    private sealed class TutorialState
    {
        public bool Seen { get; set; }
    }

    public static bool HasBeenSeen(string userKey)
    {
        try
        {
            var path = GetPath(userKey);
            if (!File.Exists(path))
                return false;

            var state = JsonSerializer.Deserialize<TutorialState>(File.ReadAllText(path));
            return state?.Seen == true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void MarkSeen(string userKey)
    {
        try
        {
            var path = GetPath(userKey);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new TutorialState { Seen = true }));
        }
        catch (IOException)
        {
            // The tutorial remains usable for the current session.
        }
        catch (UnauthorizedAccessException)
        {
            // The tutorial remains usable for the current session.
        }
    }

    private static string GetPath(string userKey)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "learn-assist", "tutorial");
        Directory.CreateDirectory(directory);

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(userKey));
        var fileKey = Convert.ToHexString(keyBytes).ToLowerInvariant();
        return Path.Combine(directory, $"{fileKey}.json");
    }
}
