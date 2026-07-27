using System;

namespace learn_Assist.Models;

public class UserDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "file";
    public string? FilePath { get; set; }
}
