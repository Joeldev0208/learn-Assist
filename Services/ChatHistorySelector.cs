using System;
using System.Collections.Generic;
using System.Linq;
using learn_Assist.Models;

namespace learn_Assist.Services;

public static class ChatHistorySelector
{
    public const int DefaultMessageLimit = 8;

    public static IReadOnlyList<ChatMessage> Select(
        IReadOnlyList<ChatMessage> history,
        int messageLimit = DefaultMessageLimit)
    {
        if (messageLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(messageLimit));

        var systemMessages = history.Where(message => message.Role == MessageRole.System);
        var conversation = history
            .Where(message => message.Role != MessageRole.System)
            .TakeLast(messageLimit);

        return systemMessages.Concat(conversation).ToList();
    }
}
