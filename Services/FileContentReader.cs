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

    public static string? ExtractText(string filePath, out string? errorReason)
    {
        errorReason = null;

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorReason = "Ruta de archivo vacía";
                return null;
            }

            if (!File.Exists(filePath))
            {
                errorReason = "El archivo no existe";
                return null;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                errorReason = $"El archivo supera los 5 MB ({fileInfo.Length / 1024 / 1024:N0} MB)";
                return null;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".pdf")
                return ExtractPdfText(filePath, out errorReason);
            if (extension == ".docx")
                return ExtractDocxText(filePath, out errorReason);
            if (extension == ".xlsx")
                return ExtractXlsxText(filePath, out errorReason);
            if (PlainTextExtensions.Contains(extension))
                return ExtractPlainText(filePath, out errorReason);

            errorReason = $"Extensión no soportada: {extension}";
            return null;
        }
        catch (Exception ex)
        {
            errorReason = $"Error inesperado: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FileContentReader] {errorReason} (archivo: {filePath})");
            return null;
        }
    }

    // Sobrecarga para compatibilidad con llamadas existentes que no necesitan el motivo
    public static string? ExtractText(string filePath)
    {
        return ExtractText(filePath, out _);
    }

    private static string? ExtractPdfText(string filePath, out string? errorReason)
    {
        errorReason = null;

        try
        {
            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            var text = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                errorReason = "El PDF no contiene texto extractable (puede ser una imagen escaneada)";
                return null;
            }

            return text;
        }
        catch (Exception ex)
        {
            errorReason = $"Error al leer PDF: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FileContentReader] {errorReason} (archivo: {filePath})");
            return null;
        }
    }

    private static string? ExtractDocxText(string filePath, out string? errorReason)
    {
        errorReason = null;

        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                errorReason = "El archivo DOCX no es válido o está corrupto";
                return null;
            }

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
            if (string.IsNullOrWhiteSpace(result))
            {
                errorReason = "El archivo DOCX no contiene texto";
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            errorReason = $"Error al leer DOCX: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FileContentReader] {errorReason} (archivo: {filePath})");
            return null;
        }
    }

    private static string? ExtractXlsxText(string filePath, out string? errorReason)
    {
        errorReason = null;

        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                errorReason = "El archivo XLSX no es válido o está corrupto";
                return null;
            }

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
            if (string.IsNullOrWhiteSpace(result))
            {
                errorReason = "El archivo XLSX no contiene texto";
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            errorReason = $"Error al leer XLSX: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FileContentReader] {errorReason} (archivo: {filePath})");
            return null;
        }
    }

    private static string? ExtractPlainText(string filePath, out string? errorReason)
    {
        errorReason = null;

        try
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            var result = text.Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                errorReason = "El archivo de texto está vacío";
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            errorReason = $"Error al leer archivo de texto: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FileContentReader] {errorReason} (archivo: {filePath})");
            return null;
        }
    }
}
