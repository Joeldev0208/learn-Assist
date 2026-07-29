using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public interface IAiService : IDisposable
{
    Task<string> AskAsync(string message, List<ChatMessage> history);
}
