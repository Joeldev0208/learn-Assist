using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class MockAiService : IAiService
{
    public void Dispose() { }

    public Task<string> AskAsync(string message, List<ChatMessage> history)
        => AskAsync(message, history, null);

    public async Task<string> AskAsync(string message, List<ChatMessage> history, IReadOnlyList<MessageAttachment>? attachments)
    {
        await Task.Delay(800);

        if (attachments is not { Count: > 0 })
            return $"You said: {message}";

        var listed = string.Join(", ", attachments.Select(a =>
            a.Kind == AttachmentKind.Image ? $"{a.FileName} (image)" : $"{a.FileName} (document)"));

        return $"You said: {message}\n\nReceived {attachments.Count} attachment(s): {listed}. " +
               "This is a mock response — configure a real AI provider to analyze them.";
    }
}
