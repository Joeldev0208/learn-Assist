using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class SessionPersistenceService
{
    private readonly string _sessionsDir;

    public SessionPersistenceService(string sessionsDir)
    {
        _sessionsDir = sessionsDir;
        if (!string.IsNullOrEmpty(_sessionsDir))
            Directory.CreateDirectory(_sessionsDir);
    }

    public string? SessionsDirectory => string.IsNullOrEmpty(_sessionsDir) ? null : _sessionsDir;

    public string GetAttachmentsDir(string sessionTitle)
    {
        var dir = Path.Combine(_sessionsDir, SanitizeFileName(sessionTitle));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string CopyImageToAttachments(string sourcePath, string sessionTitle, string fileName)
    {
        var attachmentsDir = GetAttachmentsDir(sessionTitle);
        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)) + Path.GetExtension(fileName).ToLowerInvariant();
        var destName = $"{Guid.NewGuid():N}_{safeName}";
        var destPath = Path.Combine(attachmentsDir, destName);
        if (!File.Exists(destPath))
            File.Copy(sourcePath, destPath, false);
        return destPath;
    }

    public static string CopyImageToAttachmentsStatic(string sourcePath, string sessionTitle, string fileName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sessionsDir = Path.Combine(appData, "learn-assist", "sessions");
        var dir = Path.Combine(sessionsDir, SanitizeFileName(sessionTitle));
        Directory.CreateDirectory(dir);
        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)) + Path.GetExtension(fileName).ToLowerInvariant();
        var destName = $"{Guid.NewGuid():N}_{safeName}";
        var destPath = Path.Combine(dir, destName);
        if (!File.Exists(destPath))
            File.Copy(sourcePath, destPath, false);
        return destPath;
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        if (string.IsNullOrEmpty(_sessionsDir))
            return;

        var fileName = SanitizeFileName(session.Title) + ".md";
        var filePath = Path.Combine(_sessionsDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        sb.AppendLine($"Created: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var msg in session.Messages)
        {
            sb.AppendLine($"## {msg.Role}");
            sb.AppendLine();
            if (msg.ImagePaths is { Count: > 0 })
            {
                foreach (var absPath in msg.ImagePaths)
                {
                    var relPath = Uri.EscapeDataString(Path.GetRelativePath(_sessionsDir, absPath).Replace('\\', '/'));
                    var name = Path.GetFileName(absPath);
                    sb.AppendLine($"![{Uri.EscapeDataString(name)}]({relPath})");
                }
                sb.AppendLine();
            }
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, sb.ToString());
        File.Move(tempPath, filePath, overwrite: true);
    }

    public async Task<List<ChatSession>> LoadSessionsAsync()
    {
        var sessions = new List<ChatSession>();

        if (string.IsNullOrEmpty(_sessionsDir) || !Directory.Exists(_sessionsDir))
            return sessions;

        foreach (var file in Directory.GetFiles(_sessionsDir, "*.md"))
        {
            if (file.EndsWith(".tmp"))
                continue;

            try
            {
                var lines = await File.ReadAllLinesAsync(file);
                var session = ParseSessionLines(lines, file, _sessionsDir);
                if (session is not null)
                    sessions.Add(session);
            }
            catch
            {
                // skip malformed files
            }
        }

        return sessions;
    }

    private static ChatSession? ParseSessionLines(string[] lines, string? filePath = null, string? sessionsDir = null)
    {
        if (lines.Length == 0)
            return null;

        var title = lines[0].TrimStart('#', ' ').Trim();
        var createdAt = DateTime.Now;

        foreach (var line in lines)
        {
            if (line.StartsWith("Created:", StringComparison.OrdinalIgnoreCase))
            {
                var dateStr = line["Created:".Length..].Trim();
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    createdAt = parsed;
                }
                break;
            }
        }

        var session = new ChatSession
        {
            Title = string.IsNullOrEmpty(title) && filePath is not null
                ? Path.GetFileNameWithoutExtension(filePath)
                : title,
            CreatedAt = createdAt,
        };

        MessageRole? currentRole = null;
        var contentLines = new List<string>();
        List<string>? currentImagePaths = null;

        void FlushMessage()
        {
            if (currentRole is not null && contentLines.Count > 0)
            {
                session.Messages.Add(new ChatMessage
                {
                    Role = currentRole.Value,
                    Content = string.Join("\n", contentLines).Trim(),
                    ImagePaths = currentImagePaths,
                    Timestamp = DateTime.Now,
                });
                contentLines.Clear();
                currentImagePaths = null;
            }
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("## User"))
            {
                FlushMessage();
                currentRole = MessageRole.User;
            }
            else if (line.StartsWith("## Assistant"))
            {
                FlushMessage();
                currentRole = MessageRole.Assistant;
            }
            else if (line.TrimStart().StartsWith("<!-- images:") && line.Contains("-->"))
            {
                var start = line.IndexOf("images:") + "images:".Length;
                var end = line.IndexOf("-->", start);
                if (end > start)
                {
                    var paths = line[start..end].Trim()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    currentImagePaths = paths.Length > 0 ? paths.ToList() : null;
                }
            }
            else if (line.TrimStart().StartsWith("![") && line.Contains("](") && line.TrimEnd().EndsWith(")"))
            {
                var openParen = line.IndexOf("](");
                var closeParen = line.TrimEnd().LastIndexOf(')');
                if (openParen > 0 && closeParen > openParen)
                {
                    var relPath = line[(openParen + 2)..closeParen];
                    var absPath = Path.GetFullPath(Path.Combine(sessionsDir!, Uri.UnescapeDataString(relPath)));
                    currentImagePaths ??= new List<string>();
                    if (File.Exists(absPath))
                        currentImagePaths.Add(absPath);
                }
            }
            else if (currentRole is not null && !line.StartsWith('#') && !line.StartsWith("Created:", StringComparison.OrdinalIgnoreCase))
            {
                contentLines.Add(line);
            }
        }

        FlushMessage();

        return session.Messages.Count > 0 ? session : null;
    }

    public async Task<ChatSession?> LoadSessionAsync(string title)
    {
        var fileName = SanitizeFileName(title) + ".md";
        var filePath = Path.Combine(_sessionsDir, fileName);
        if (!File.Exists(filePath))
            return null;

        var lines = await File.ReadAllLinesAsync(filePath);
        return ParseSessionLines(lines, filePath, _sessionsDir);
    }

    public string GetSessionFilePath(string title)
    {
        var fileName = SanitizeFileName(title) + ".md";
        return Path.Combine(_sessionsDir, fileName);
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "session" : sanitized.Trim();
    }
}
