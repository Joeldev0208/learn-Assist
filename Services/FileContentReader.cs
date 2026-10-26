using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace learn_Assist.Services;

public static class FileContentReader
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly string[] PlainTextExtensions =
    [
        ".txt", ".md", ".cs", ".py", ".js", ".ts", ".json", ".csv", ".xml", ".html", ".htm",
        ".css", ".sql", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".rb", ".php",
        ".swift", ".kt", ".kts", ".scala", ".sh", ".bat", ".ps1", ".yaml", ".yml", ".toml",
        ".ini", ".cfg", ".conf", ".log", ".rst", ".tex", ".bib", ".env", ".gitignore",
        ".dockerignore", ".editorconfig", ".csproj", ".sln", ".props", ".targets",
    ];

    public static string? ExtractText(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
                return null;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => ExtractPdfText(filePath),
                ".docx" => ExtractDocxText(filePath),
                ".xlsx" => ExtractXlsxText(filePath),
                _ => PlainTextExtensions.Contains(extension) ? ExtractPlainText(filePath) : null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractPdfText(string filePath)
    {
        try
        {
            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            var text = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractDocxText(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
                return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var xml = reader.ReadToEnd();

            var doc = XDocument.Parse(xml);
            var sb = new StringBuilder();

            foreach (var paragraph in doc.Descendants("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}p"))
            {
                var text = string.Join("", paragraph.Descendants("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t")
                    .Select(t => t.Value));
                sb.AppendLine(text);
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractXlsxText(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
                return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var xml = reader.ReadToEnd();

            var doc = XDocument.Parse(xml);
            var ns = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}";
            var sb = new StringBuilder();

            foreach (var si in doc.Descendants($"{ns}si"))
            {
                var text = string.Join("", si.Descendants($"{ns}t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractPlainText(string filePath)
    {
        try
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            var result = text.Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }
}
