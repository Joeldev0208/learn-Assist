using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using System.Collections.Generic;

namespace learn_Assist.Services;

public static class FileContentReader
{
    /// <summary>
    /// Tamaño máximo del archivo en disco. Es solo una protección contra archivos
    /// absurdos: no limita el texto extraído.
    /// </summary>
    private const long MaxFileSize = 200 * 1024 * 1024;

    /// <summary>
    /// Máximo de caracteres extraídos por documento. Un libro completo no cabe en la
    /// ventana de contexto de la IA, así que se trunca y se marca en el propio texto.
    /// </summary>
    public const int MaxExtractedCharacters = 150_000;

    private const string TruncationNotice =
        "\n\n[... contenido truncado: el documento original es más largo de lo que se puede enviar al modelo ...]";

    private static readonly Regex PercentEscapePattern = new("%[0-9A-Fa-f]{2}", RegexOptions.Compiled);

    private static readonly string[] PlainTextExtensions =
    [
        ".txt", ".md", ".cs", ".py", ".js", ".ts", ".json", ".csv", ".xml", ".html", ".htm",
        ".css", ".sql", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".rb", ".php",
        ".swift", ".kt", ".kts", ".scala", ".sh", ".bat", ".ps1", ".yaml", ".yml", ".toml",
        ".ini", ".cfg", ".conf", ".log", ".rst", ".tex", ".bib", ".env", ".gitignore",
        ".dockerignore", ".editorconfig", ".csproj", ".sln", ".props", ".targets",
    ];

    /// <summary>
    /// Convierte una ruta guardada como URI o con escapes percentuales (%20, %C3%AD…)
    /// en una ruta de archivo utilizable por <see cref="File"/>. Las URLs web no deben
    /// pasar por aquí: se decodificarían y dejarían de ser válidas.
    /// </summary>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path ?? string.Empty;

        var result = path.Trim();

        if (result.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(result, UriKind.Absolute, out var uri))
        {
            result = uri.LocalPath;
        }

        if (PercentEscapePattern.IsMatch(result))
            result = Uri.UnescapeDataString(result);

        // Uri.AbsolutePath deja "/C:/carpeta/..." en Windows.
        if (result.Length >= 3 && result[0] == '/' && char.IsLetter(result[1]) && result[2] == ':')
            result = result[1..];

        return result;
    }

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

            var path = NormalizePath(filePath);

            if (!File.Exists(path))
            {
                errorReason = "El archivo no existe";
                return null;
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > MaxFileSize)
            {
                errorReason = $"El archivo supera el límite de {MaxFileSize / 1024 / 1024} MB ({fileInfo.Length / 1024 / 1024:N0} MB)";
                return null;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();

            string? result;
            if (extension == ".pdf")
                result = ExtractPdfText(path, out errorReason);
            else if (extension == ".docx")
                result = ExtractDocxText(path, out errorReason);
            else if (extension == ".xlsx")
                result = ExtractXlsxText(path, out errorReason);
            else if (PlainTextExtensions.Contains(extension))
                result = ExtractPlainText(path, out errorReason);
            else
            {
                errorReason = $"Extensión no soportada: {extension}";
                return null;
            }

            return Truncate(result);
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

    public static PdfPagesResult ExtractPdfPages(string filePath)
    {
        try
        {
            var path = NormalizePath(filePath);
            if (!File.Exists(path))
                return PdfPagesResult.Failed("El archivo PDF no existe.");

            using var document = PdfDocument.Open(path);
            var pages = document.GetPages()
                .Select(page => string.IsNullOrWhiteSpace(page.Text)
                    ? "[Esta página no contiene texto extraíble.]"
                    : page.Text.Trim())
                .ToList();

            return pages.Count == 0
                ? PdfPagesResult.Failed("El PDF no contiene páginas.")
                : PdfPagesResult.Succeeded(pages);
        }
        catch (Exception ex)
        {
            return PdfPagesResult.Failed($"Error al leer el PDF: {ex.Message}");
        }
    }

    private static string? Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxExtractedCharacters)
            return text;

        return string.Concat(text.AsSpan(0, MaxExtractedCharacters), TruncationNotice);
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
                // No seguir leyendo un libro de 500 páginas si ya no se enviará completo.
                if (sb.Length >= MaxExtractedCharacters)
                    break;
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

    public sealed record PdfPagesResult(bool Success, IReadOnlyList<string> Pages, string? Error)
    {
        public static PdfPagesResult Succeeded(IReadOnlyList<string> pages) => new(true, pages, null);
        public static PdfPagesResult Failed(string error) => new(false, [], error);
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
                if (sb.Length >= MaxExtractedCharacters)
                    break;
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
                if (sb.Length >= MaxExtractedCharacters)
                    break;
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
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Leer como máximo un carácter más que el límite, para no cargar en memoria
            // archivos de texto enormes solo para descartarlos después.
            var buffer = new char[MaxExtractedCharacters + 1];
            var total = 0;

            while (total < buffer.Length)
            {
                var read = reader.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }

            var result = new string(buffer, 0, total).Trim();
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
