using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class MockAiService : IAiService
{
    public void Dispose() { }
    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        await Task.Delay(800);
    return $"You said: {message}";

    }
}
