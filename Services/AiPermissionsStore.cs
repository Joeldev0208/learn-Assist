using System;
using System.IO;
using System.Text.Json;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Persiste los permisos de la IA en un archivo JSON pequeño, separado de
/// <c>documents.json</c> y de la configuración cifrada del proveedor.
///
/// La lectura es tolerante a fallos: un archivo dañado devuelve los valores
/// por defecto en lugar de impedir que la app arranque. La escritura es
/// atómica y best-effort, igual que <see cref="ResourceModeStore"/>.
/// </summary>
public sealed class AiPermissionsStore
{
    /// <summary>
    /// Ruta real de la preferencia. Los tests deben inyectar una ruta temporal
    /// en el constructor para no sobrescribir la del usuario.
    /// </summary>
    public static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "ai-permissions.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;

    public AiPermissionsStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath;
    }

    /// <summary>
    /// Lee los permisos. Si el archivo no existe, está dañado o no se puede
    /// leer, devuelve <see cref="AiPermissions.Default"/>: el modo restrictivo
    /// es el punto de partida seguro.
    /// </summary>
    public AiPermissions Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return AiPermissions.Default;

            var json = File.ReadAllText(_filePath);
            var permissions = JsonSerializer.Deserialize<AiPermissions>(json, JsonOptions);
            return permissions ?? AiPermissions.Default;
        }
        catch
        {
            // Una preferencia ilegible no debe impedir usar la app.
            return AiPermissions.Default;
        }
    }

    /// <summary>
    /// Guarda los permisos de forma atómica. Es best-effort: un fallo de disco
    /// nunca debe romper el chat.
    /// </summary>
    public void Save(AiPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(permissions, JsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            // Persistencia best-effort.
        }
    }
}
