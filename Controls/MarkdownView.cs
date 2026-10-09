using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;

namespace learn_Assist.Controls;

public sealed class MarkdownView : UserControl
{
    public static event Action? RenderCompleted;

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private readonly StackPanel _content = new() { Spacing = 8 };

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public MarkdownView()
    {
        Content = _content;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
        AttachedToVisualTree += (_, _) => Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
            Render();
    }

    private void Render()
    {
        _content.Children.Clear();

        var text = Markdown ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            RenderCompleted?.Invoke();
            return;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new List<string>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);
                var code = new StringBuilder();
                index++;

                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    if (code.Length > 0)
                        code.AppendLine();
                    code.Append(lines[index]);
                    index++;
                }

                AddCodeBlock(code.ToString());
                if (index < lines.Length)
                    index++;
                continue;
            }

            if (line.Trim() == "$$")
            {
                FlushParagraph(paragraph);
                var formula = new StringBuilder();
                index++;

                while (index < lines.Length && lines[index].Trim() != "$$")
                {
                    if (formula.Length > 0)
                        formula.AppendLine();
                    formula.Append(lines[index].Trim());
                    index++;
                }

                AddFormulaBlock(formula.ToString());
                if (index < lines.Length)
                    index++;
                continue;
            }

            if (line.Trim() == @"\[")
            {
                FlushParagraph(paragraph);
                var formula = new StringBuilder();
                index++;

                while (index < lines.Length && lines[index].Trim() != @"\]")
                {
                    if (formula.Length > 0)
                        formula.AppendLine();
                    formula.Append(lines[index].Trim());
                    index++;
                }

                AddFormulaBlock(formula.ToString());
                if (index < lines.Length)
                    index++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraph);
                index++;
                continue;
            }

            if (TryGetHeading(line, out var level, out var heading))
            {
                FlushParagraph(paragraph);
                AddInlineText(heading, GetHeadingSize(level), FontWeight.Bold);
                index++;
                continue;
            }

            if (IsHorizontalRule(line))
            {
                FlushParagraph(paragraph);
                _content.Children.Add(new Border
                {
                    Height = 1,
                    Background = FindBrush("DividerBrush"),
                    Margin = new Thickness(0, 4),
                });
                index++;
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal) || line == ">")
            {
                FlushParagraph(paragraph);
                AddQuote(line.Length > 1 ? line[1..].TrimStart() : string.Empty);
                index++;
                continue;
            }

            if (TryGetListItem(line, out var marker, out var item))
            {
                FlushParagraph(paragraph);
                var list = new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 0, 0) };
                var listLines = new List<(string Marker, string Text)> { (marker, item) };
                index++;

                while (index < lines.Length && TryGetListItem(lines[index], out marker, out item))
                {
                    listLines.Add((marker, item));
                    index++;
                }

                foreach (var listLine in listLines)
                    list.Children.Add(CreateListItem(listLine.Marker, listLine.Text));

                _content.Children.Add(list);
                continue;
            }

            paragraph.Add(line);
            index++;
        }

        FlushParagraph(paragraph);
        RenderCompleted?.Invoke();
    }

    private void FlushParagraph(List<string> paragraph)
    {
        if (paragraph.Count == 0)
            return;

        AddInlineText(string.Join("\n", paragraph), 14, FontWeight.Normal);
        paragraph.Clear();
    }

    private void AddInlineText(string text, double fontSize, FontWeight weight)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = FindBrush("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = fontSize + 8,
        };

        foreach (var inline in ParseInline(text))
        {
            if (inline.Formula)
            {
                block.Inlines!.Add(new Run(FormatFormula(inline.Text))
                {
                    FontSize = fontSize,
                    FontFamily = new FontFamily("Cambria Math"),
                    Foreground = FindBrush("MarkdownFormulaForegroundBrush") ?? FindBrush("TextPrimaryBrush"),
                });
                continue;
            }

            var run = new Run(inline.Text)
            {
                FontWeight = inline.Bold ? FontWeight.Bold : weight,
                FontStyle = inline.Italic ? FontStyle.Italic : FontStyle.Normal,
                TextDecorations = inline.Strikethrough ? TextDecorations.Strikethrough : null,
                Foreground = inline.Code ? FindBrush("MarkdownCodeForegroundBrush") : FindBrush("TextPrimaryBrush"),
                FontFamily = inline.Code ? new FontFamily("monospace") : FontFamily.Default,
            };
            block.Inlines!.Add(run);
        }

        _content.Children.Add(block);
    }

    private void AddCodeBlock(string code)
    {
        var codeText = new TextBlock
        {
            Text = code,
            FontSize = 13,
            FontFamily = new FontFamily("monospace"),
            Foreground = FindBrush("MarkdownCodeForegroundBrush"),
            TextWrapping = TextWrapping.NoWrap,
        };

        var scroll = new ScrollViewer
        {
            Content = codeText,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        _content.Children.Add(new Border
        {
            Background = FindBrush("MarkdownCodeBackgroundBrush"),
            BorderBrush = FindBrush("MarkdownCodeBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Child = scroll,
        });
    }

    private void AddFormulaBlock(string formula)
    {
        _content.Children.Add(new Border
        {
            Background = FindBrush("MarkdownFormulaBackgroundBrush"),
            BorderBrush = FindBrush("MarkdownFormulaBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            Child = new TextBlock
            {
                Text = FormatFormula(formula),
                FontSize = 17,
                FontFamily = new FontFamily("Cambria Math"),
                Foreground = FindBrush("MarkdownFormulaForegroundBrush") ?? FindBrush("TextPrimaryBrush"),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            },
        });
    }

    private Control CreateListItem(string marker, string text)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*") };
        row.Children.Add(new TextBlock
        {
            Text = marker,
            FontSize = 14,
            Foreground = FindBrush("TextSecondaryBrush"),
        });

        var body = CreateInlineTextBlock(text);
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        return row;
    }

    private void AddQuote(string text)
    {
        var body = CreateInlineTextBlock(text);
        _content.Children.Add(new Border
        {
            BorderBrush = FindBrush("MarkdownQuoteBorderBrush"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = body,
        });
    }

    private TextBlock CreateInlineTextBlock(string text)
    {
        var block = new TextBlock
        {
            FontSize = 14,
            Foreground = FindBrush("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
        };

        foreach (var inline in ParseInline(text))
        {
            if (inline.Formula)
            {
                block.Inlines!.Add(new Run(FormatFormula(inline.Text))
                {
                    FontSize = 14,
                    FontFamily = new FontFamily("Cambria Math"),
                    Foreground = FindBrush("MarkdownFormulaForegroundBrush") ?? FindBrush("TextPrimaryBrush"),
                });
                continue;
            }

            block.Inlines!.Add(new Run(inline.Text)
            {
                FontWeight = inline.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = inline.Italic ? FontStyle.Italic : FontStyle.Normal,
                TextDecorations = inline.Strikethrough ? TextDecorations.Strikethrough : null,
                Foreground = inline.Code ? FindBrush("MarkdownCodeForegroundBrush") : FindBrush("TextPrimaryBrush"),
                FontFamily = inline.Code ? new FontFamily("monospace") : FontFamily.Default,
            });
        }

        return block;
    }

    private static IBrush? FindBrush(string key)
    {
        if (Application.Current is { } app
            && app.Resources.TryGetResource(key, app.RequestedThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return null;
    }

    private static bool TryGetHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var trimmed = line.TrimStart();
        while (level < 6 && level < trimmed.Length && trimmed[level] == '#')
            level++;

        if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
            return false;

        text = trimmed[(level + 1)..].Trim();
        return text.Length > 0;
    }

    private static bool TryGetListItem(string line, out string marker, out string text)
    {
        marker = string.Empty;
        text = string.Empty;
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            marker = "•";
            text = trimmed[2..].Trim();
            return true;
        }

        var dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 0 && trimmed[..dot].All(char.IsDigit))
        {
            marker = trimmed[..(dot + 1)];
            text = trimmed[(dot + 2)..].Trim();
            return true;
        }

        return false;
    }

    private static bool IsHorizontalRule(string line)
    {
        var compact = line.Replace(" ", string.Empty);
        return compact is "---" or "***" or "___";
    }

    private static double GetHeadingSize(int level) => level switch
    {
        1 => 24,
        2 => 20,
        3 => 17,
        _ => 15,
    };

    private static string FormatFormula(string formula)
    {
        var result = formula.Trim();
        result = result.Replace(@"\cdot", "·", StringComparison.Ordinal);
        result = result.Replace(@"\times", "×", StringComparison.Ordinal);
        result = result.Replace(@"\div", "÷", StringComparison.Ordinal);
        result = result.Replace(@"\leq", "≤", StringComparison.Ordinal);
        result = result.Replace(@"\geq", "≥", StringComparison.Ordinal);
        result = result.Replace(@"\neq", "≠", StringComparison.Ordinal);
        result = result.Replace(@"\pm", "±", StringComparison.Ordinal);
        result = result.Replace(@"\infty", "∞", StringComparison.Ordinal);
        result = result.Replace(@"\rightarrow", "→", StringComparison.Ordinal);
        result = result.Replace(@"\Rightarrow", "⇒", StringComparison.Ordinal);
        result = result.Replace(@"\alpha", "α", StringComparison.Ordinal);
        result = result.Replace(@"\beta", "β", StringComparison.Ordinal);
        result = result.Replace(@"\gamma", "γ", StringComparison.Ordinal);
        result = result.Replace(@"\delta", "δ", StringComparison.Ordinal);
        result = result.Replace(@"\lambda", "λ", StringComparison.Ordinal);
        result = result.Replace(@"\mu", "μ", StringComparison.Ordinal);
        result = result.Replace(@"\pi", "π", StringComparison.Ordinal);
        result = result.Replace(@"\sigma", "σ", StringComparison.Ordinal);
        result = result.Replace(@"\theta", "θ", StringComparison.Ordinal);
        result = result.Replace(@"\phi", "φ", StringComparison.Ordinal);
        result = result.Replace(@"\sum", "∑", StringComparison.Ordinal);
        result = result.Replace(@"\int", "∫", StringComparison.Ordinal);
        result = result.Replace(@"\sqrt", "√", StringComparison.Ordinal);
        result = result.Replace(@"\left", string.Empty, StringComparison.Ordinal);
        result = result.Replace(@"\right", string.Empty, StringComparison.Ordinal);
        result = result.Replace(@"\,", " ", StringComparison.Ordinal);
        result = result.Replace("{", string.Empty, StringComparison.Ordinal)
                       .Replace("}", string.Empty, StringComparison.Ordinal);

        return result;
    }

    private static IEnumerable<InlinePart> ParseInline(string text)
    {
        var parts = new List<InlinePart>();
        var buffer = new StringBuilder();
        var index = 0;

        void Flush()
        {
            if (buffer.Length > 0)
            {
                parts.Add(new InlinePart(buffer.ToString()));
                buffer.Clear();
            }
        }

        while (index < text.Length)
        {
            if (text[index] == '$')
            {
                var end = text.IndexOf('$', index + 1);
                if (end > index + 1)
                {
                    Flush();
                    parts.Add(new InlinePart(text[(index + 1)..end], Formula: true));
                    index = end + 1;
                    continue;
                }
            }

            if (text.AsSpan(index).StartsWith(@"\(".AsSpan(), StringComparison.Ordinal))
            {
                var end = text.IndexOf(@"\)", index + 2, StringComparison.Ordinal);
                if (end > index + 2)
                {
                    Flush();
                    parts.Add(new InlinePart(text[(index + 2)..end], Formula: true));
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end > index + 1)
                {
                    Flush();
                    parts.Add(new InlinePart(text[(index + 1)..end], Code: true));
                    index = end + 1;
                    continue;
                }
            }

            if (text.AsSpan(index).StartsWith("**".AsSpan(), StringComparison.Ordinal)
                || text.AsSpan(index).StartsWith("__".AsSpan(), StringComparison.Ordinal))
            {
                var delimiter = text.Substring(index, 2);
                var end = text.IndexOf(delimiter, index + 2, StringComparison.Ordinal);
                if (end > index + 2)
                {
                    Flush();
                    parts.Add(new InlinePart(text[(index + 2)..end], Bold: true));
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] is '*' or '_' or '~')
            {
                var delimiter = text[index] == '~' ? "~~" : text[index].ToString();
                if (text.AsSpan(index).StartsWith(delimiter.AsSpan(), StringComparison.Ordinal))
                {
                    var end = text.IndexOf(delimiter, index + delimiter.Length, StringComparison.Ordinal);
                    if (end > index + delimiter.Length)
                    {
                        Flush();
                        parts.Add(new InlinePart(
                            text[(index + delimiter.Length)..end],
                            Italic: delimiter.Length == 1,
                            Strikethrough: delimiter == "~~"));
                        index = end + delimiter.Length;
                        continue;
                    }
                }
            }

            buffer.Append(text[index]);
            index++;
        }

        Flush();
        return parts;
    }

    private readonly record struct InlinePart(
        string Text,
        bool Bold = false,
        bool Italic = false,
        bool Strikethrough = false,
        bool Code = false,
        bool Formula = false);
}
