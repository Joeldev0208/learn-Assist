using System;
using System.IO;

namespace learn_Assist.Services;

public static class DotEnv
{
    public static void Load(string path = ".env")
    {
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var eq = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
                continue;

            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
