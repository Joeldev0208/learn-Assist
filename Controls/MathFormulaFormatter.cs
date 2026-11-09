using System;
using System.Text.RegularExpressions;

namespace learn_Assist.Controls;

public static partial class MathFormulaFormatter
{
    public static string Format(string formula)
    {
        var result = formula.Trim()
            .Replace("\\\\", "\n", StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal);

        result = ReplaceRepeated(result, FractionRegex(), "($1)/($2)");
        result = ReplaceRepeated(result, SqrtRegex(), "√($1)");
        result = ReplaceRepeated(result, FractionRegex(), "($1)/($2)");
        result = ReplaceRepeated(result, TextCommandRegex(), "$1");
        result = ReplaceRepeated(result, GroupedPowerRegex(), "$1^($2)");
        result = ReplaceRepeated(result, GroupedSubscriptRegex(), "$1_($2)");

        result = result
            .Replace(@"\left", string.Empty, StringComparison.Ordinal)
            .Replace(@"\right", string.Empty, StringComparison.Ordinal)
            .Replace(@"\,", " ", StringComparison.Ordinal)
            .Replace(@"\;", " ", StringComparison.Ordinal)
            .Replace(@"\!", string.Empty, StringComparison.Ordinal);

        foreach (var (command, symbol) in Symbols)
            result = result.Replace(command, symbol, StringComparison.Ordinal);

        result = CommandRegex().Replace(result, "$1");
        return result.Replace("{", "(", StringComparison.Ordinal)
            .Replace("}", ")", StringComparison.Ordinal)
            .Trim();
    }

    private static string ReplaceRepeated(
        string value,
        Regex regex,
        string replacement)
    {
        string updated;
        do
        {
            updated = regex.Replace(value, replacement);
            if (updated == value)
                return value;
            value = updated;
        } while (true);
    }

    private static readonly (string Command, string Symbol)[] Symbols =
    [
        (@"\cdot", "·"), (@"\times", "×"), (@"\div", "÷"),
        (@"\leq", "≤"), (@"\geq", "≥"), (@"\neq", "≠"),
        (@"\pm", "±"), (@"\infty", "∞"), (@"\rightarrow", "→"),
        (@"\Rightarrow", "⇒"), (@"\alpha", "α"), (@"\beta", "β"),
        (@"\gamma", "γ"), (@"\delta", "δ"), (@"\lambda", "λ"),
        (@"\mu", "μ"), (@"\pi", "π"), (@"\sigma", "σ"),
        (@"\theta", "θ"), (@"\phi", "φ"), (@"\sum", "∑"),
        (@"\int", "∫"),
    ];

    [GeneratedRegex(@"\\frac\s*\{([^{}]*)\}\s*\{([^{}]*)\}")]
    private static partial Regex FractionRegex();

    [GeneratedRegex(@"\\sqrt\s*\{([^{}]*)\}")]
    private static partial Regex SqrtRegex();

    [GeneratedRegex(@"\\(?:text|mathrm|mathbf|mathit|operatorname)\s*\{([^{}]*)\}")]
    private static partial Regex TextCommandRegex();

    [GeneratedRegex(@"([A-Za-z0-9)])\^\{([^{}]+)\}")]
    private static partial Regex GroupedPowerRegex();

    [GeneratedRegex(@"([A-Za-z0-9)])_\{([^{}]+)\}")]
    private static partial Regex GroupedSubscriptRegex();

    [GeneratedRegex(@"\\([A-Za-z]+)")]
    private static partial Regex CommandRegex();
}
