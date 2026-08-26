using System;
using System.Collections.Generic;

namespace learn_Assist.Models;

public enum MessageRole
{
    User,
    Assistant,
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string>? ImagePaths { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
