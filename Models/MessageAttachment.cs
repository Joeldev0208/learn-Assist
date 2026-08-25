namespace learn_Assist.Models;

public enum AttachmentKind
{
    Image,
    TextDocument,
}

public class MessageAttachment
{
    public string FileName { get; set; } = string.Empty;
    public AttachmentKind Kind { get; set; }
    public string MimeType { get; set; } = "application/octet-stream";
    public byte[]? ImageData { get; set; }
    public string? TextContent { get; set; }

    public string ToTextBlock()
        => $"[Attached document: {FileName}]\n{TextContent}\n[/Attached document]";
}
