using System;

namespace learn_Assist.Models;

public enum DocumentContentType
{
    Document,
    Image,
    Video,
}

public class UserDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "file";
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public DocumentContentType ContentType { get; set; } = DocumentContentType.Document;
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public string? LocalPath { get; set; }

    public string SizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F1} MB",
    };
}
