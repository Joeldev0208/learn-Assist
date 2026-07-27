using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class MockAiService : IAiService
{
    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        await Task.Delay(800);

        return $$"""
                Sure. Here is a code block for your "{{message}}" project. It is built using React, and uses the local time for London, England as standard. Let me know if you would like to make any refinements to the code.

                ```typescript
                import React, { useState, useEffect } from 'react';

                const AnalogClock: React.FC = () => {
                  const [time, setTime] = useState(new Date());

                  useEffect(() => {
                    const timer = setInterval(() => {
                      setTime(new Date());
                    }, 1000);
                    return () => clearInterval(timer);
                  }, []);

                  return (
                    <div className="clock">
                      <span>{time.toTimeString()}</span>
                    </div>
                  );
                };

                export default AnalogClock;
                ```

                Would you like me to add anything else?
                """;
    }
}
