using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public sealed class LearningProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _profilesDirectory;

    public LearningProfileService(string? storageDirectory = null)
    {
        var directory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "learn-assist");

        _profilesDirectory = Path.Combine(directory, "learning-profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task<LearningProfile?> LoadAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var path = GetProfilePath(userId);
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LearningProfile>(stream, JsonOptions);
    }

    public async Task SaveAsync(LearningProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.UserId);

        profile.UpdatedAtUtc = DateTime.UtcNow;
        var path = GetProfilePath(profile.UserId);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, profile, JsonOptions);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    public Task DeleteAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var path = GetProfilePath(userId);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string GetProfilePath(string userId)
    {
        var userKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userId.Trim())));
        return Path.Combine(_profilesDirectory, $"{userKey}.json");
    }
}
