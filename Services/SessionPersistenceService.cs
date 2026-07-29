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
                var session = ParseSessionLines(lines, file);
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

    private static ChatSession? ParseSessionLines(string[] lines, string? filePath = null)
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

        void FlushMessage()
        {
            if (currentRole is not null && contentLines.Count > 0)
            {
                session.Messages.Add(new ChatMessage
                {
                    Role = currentRole.Value,
                    Content = string.Join("\n", contentLines).Trim(),
                    Timestamp = DateTime.Now,
                });
                contentLines.Clear();
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
        return ParseSessionLines(lines, filePath);
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
