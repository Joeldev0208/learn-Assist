using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
    private const long MaxContentSize = 5 * 1024 * 1024; // 5 MB, igual que FileContentReader

    private static readonly Regex HtmlTagRegex = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex HtmlScriptRegex = new("<script.*?</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlStyleRegex = new("<style.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    // Hosts bloqueados para evitar SSRF (acceso a servicios internos/metadatos de nube)
    private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "::1", "0.0.0.0",
        "169.254.169.254", // metadatos AWS / Azure
        "169.254.169.250", // metadatos Azure
        "100.100.100.200", // metadatos Alibaba Cloud
    };

    /// <summary>
    /// Crea una nueva instancia con el <see cref="HttpClient"/> dado.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP opcional (para pruebas).</param>
    /// <param name="timeout">Tiempo de espera opcional (por defecto 20 segundos).</param>
    public WebContentReader(HttpClient? httpClient = null, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _httpClient.Timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
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
    /// Descarga el contenido de una URL y devuelve el texto extraído junto con
    /// información sobre el éxito o el motivo del fallo.
    /// </summary>
    /// <param name="url">URL completa (http:// o https://).</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <returns>Resultado con el texto extraído o el motivo del error.</returns>
    public async Task<WebContentFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return WebContentFetchResult.Fail("La URL no puede estar vacía.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return WebContentFetchResult.Fail("La URL no es válida.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return WebContentFetchResult.Fail("Solo se admiten URLs http:// o https://.");

        if (!await IsSafeUriAsync(uri, cancellationToken))
            return WebContentFetchResult.Fail("No se puede acceder a hosts locales o internos.");

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);

            if (response.RequestMessage?.RequestUri is { } finalUri
                && !await IsSafeUriAsync(finalUri, cancellationToken))
            {
                return WebContentFetchResult.Fail("La redirección apunta a un host local o interno.");
            }

            if (!response.IsSuccessStatusCode)
                return WebContentFetchResult.Fail($"El servidor respondió {(int)response.StatusCode} {response.StatusCode}.");

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!IsSupportedContentType(contentType))
                return WebContentFetchResult.Fail($"Tipo de contenido no admitido: {contentType}.");

            // Leer por streaming con límite de tamaño (evita OOM en páginas enormes)
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[8192];
            using var ms = new MemoryStream();
            int read;
            long total = 0;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxContentSize)
                    return WebContentFetchResult.Fail($"El contenido supera el límite de {MaxContentSize / 1024 / 1024} MB.");
                await ms.WriteAsync(buffer, 0, read, cancellationToken);
            }

            var html = Encoding.UTF8.GetString(ms.ToArray());
            return WebContentFetchResult.Ok(ExtractText(html));
        }
        catch (OperationCanceledException)
        {
            return WebContentFetchResult.Fail("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return WebContentFetchResult.Fail("No se pudo descargar el contenido.");
        }
        catch (Exception)
        {
            return WebContentFetchResult.Fail("Ocurrió un error al procesar el contenido.");
        }
    }

    private static async Task<bool> IsSafeUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (BlockedHosts.Contains(uri.Host))
            return false;

        if (IPAddress.TryParse(uri.Host, out var address))
            return IsPublicAddress(address);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            return addresses.Length > 0 && addresses.All(IsPublicAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast)
            || address.AddressFamily == AddressFamily.InterNetworkV6
                && (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                    || address.GetAddressBytes()[0] is 0xfc or 0xfd)
            || address.AddressFamily == AddressFamily.InterNetwork
                && IsPrivateIpv4(address))
        {
            return false;
        }

        return true;
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
            || bytes[0] == 192 && bytes[1] == 168
            || bytes[0] == 169 && bytes[1] == 254
            || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
            || bytes[0] == 127
            || bytes[0] == 0;
    }

    /// <summary>
    /// Sobrecarga de compatibilidad hacia atrás: devuelve solo el texto (o null si falla).
    /// </summary>
    public async Task<string?> FetchTextAsync(string url)
    {
        var result = await FetchAsync(url);
        return result.Text;
    }

    private static bool IsSupportedContentType(string contentType)
    {
        var ct = contentType.ToLowerInvariant();
        return ct.Contains("text")
            || ct.Contains("html")
            || ct.Contains("xhtml")
            || ct.Contains("xml")
            || ct.Contains("json");
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
