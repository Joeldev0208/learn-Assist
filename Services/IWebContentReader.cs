using System.Threading;
using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Resultado de la lectura de contenido web. Permite al llamador saber
/// exactamente por qu\u00e9 fall\u00f3 una descarga (c\u00f3digo HTTP, red, tama\u00f1o, etc.).
/// </summary>
/// <param name="Text">Texto extraído cuando <see cref="Success"/> es true.</param>
/// <param name="Error">Motivo del error cuando <see cref="Success"/> es false.</param>
/// <param name="Success">Indica si la descarga y extracción tuvieron éxito.</param>
public record WebContentFetchResult(string? Text, string? Error, bool Success)
{
    public static WebContentFetchResult Ok(string text) => new(text, null, true);
    public static WebContentFetchResult Fail(string error) => new(null, error, false);
}

/// <summary>
/// Contrato para leer contenido de una URL y extraer texto plano.
/// Permite mockeo en pruebas unitarias sin hacer llamadas de red reales.
/// </summary>
public interface IWebContentReader
{
    /// <summary>
    /// Descarga el contenido de una URL y devuelve el texto extraído.
    /// </summary>
    /// <param name="url">URL completa (http:// o https://).</param>
    /// <param name="cancellationToken">Token para cancelar la operaci\u00f3n.</param>
    Task<WebContentFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default);
}
