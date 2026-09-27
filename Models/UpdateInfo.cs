using System;

namespace learn_Assist.Models;

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; init; }
    public Version LatestVersion { get; init; } = new();
    public string ReleaseNotes { get; init; } = string.Empty;
}