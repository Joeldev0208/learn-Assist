using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Descarga el contenido de una URL y extrae texto plano simple
/// para usarlo como material de examen sin depender de APIs de búsqueda pagas.
/// </summary>
public class WebContentReader : IWebContentReader
{
    private static readonly IWebContentReader _instance = new WebContentReader();

    /// <summary>
    /// Instancia predeterminada para uso sin inyección de dependencias.
    /// </summary>
    public static IWebContentReader Instance => _instance;

    private readonly HttpClient _httpClient;

    private static readonly Regex HtmlTagRegex = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex HtmlScriptRegex = new("<script.*?</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlStyleRegex = new("<style.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    /// <summary>
    /// Crea una nueva instancia con el <see cref="HttpClient"/> dado.
    /// </summary>
    public WebContentReader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // Seguir redirecciones automáticamente
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            UseCookies = false,
        };

        var client = new HttpClient(handler);
        // User-Agent de navegador real para evitar bloqueos de sitios web
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
        client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

        return client;
    }

    /// <summary>
    /// Descarga el contenido de una URL y devuelve el texto extraído.
    /// </summary>
    /// <param name="url">URL completa (http:// o https://).</param>
    /// <returns>Texto plano extraído del HTML, o null si falla.</returns>
    public async Task<string?> FetchTextAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            // Aceptar HTML y texto plano; rechazar binarios (PDF, imágenes, etc.)
            if (!contentType.Contains("text", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();
            return ExtractText(html);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrae texto plano de un HTML básico (quita etiquetas y normaliza espacios).
    /// Prioriza el contenido semántico (article, main, párrafos, encabezados) cuando está disponible.
    /// Es <see langword="static"/> para poder probarse sin instanciar la clase.
    /// </summary>
    public static string ExtractText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = HtmlScriptRegex.Replace(html, " ");
        text = HtmlStyleRegex.Replace(text, " ");

        // Intentar extraer solo el contenido semántico si existe
        var semanticMatch = Regex.Match(text, @"<(article|main)[\s\S]*?</\1>", RegexOptions.IgnoreCase);
        if (semanticMatch.Success && semanticMatch.Length > 100)
        {
            text = semanticMatch.Value;
        }
        else
        {
            // Fallback: extraer el body si existe
            var bodyMatch = Regex.Match(text, @"<body[\s\S]*?(</body>)", RegexOptions.IgnoreCase);
            if (bodyMatch.Success && bodyMatch.Length > 100)
            {
                text = bodyMatch.Value;
            }
        }

        text = HtmlTagRegex.Replace(text, " ");

        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");

        text = WhitespaceRegex.Replace(text, " ").Trim();

        return text;
    }
}
