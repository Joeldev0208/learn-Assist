using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public interface IAiService
{
    Task<string> AskAsync(string message, List<ChatMessage> history);
}
