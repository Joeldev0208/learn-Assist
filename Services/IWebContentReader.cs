using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Contrato para leer contenido de una URL y extraer texto plano.
/// Permite mockeo en pruebas unitarias sin hacer llamadas de red reales.
/// </summary>
public interface IWebContentReader
{
    Task<string?> FetchTextAsync(string url);
}
