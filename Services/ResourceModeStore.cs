using System;
using System.IO;
using System.Text.Json;

namespace learn_Assist.Services;

/// <summary>
/// Persiste el interruptor «Solo recursos» del chat en un archivo JSON pequeño.
/// Se mantiene separado de <c>documents.json</c> para no cambiar el esquema de
/// la biblioteca de documentos ni romper los archivos ya existentes.
/// </summary>
public sealed class ResourceModeStore
{
    /// <summary>
    /// Ruta real de la preferencia. Los tests deben inyectar una ruta temporal
    /// en el constructor para no sobrescribir la del usuario.
    /// </summary>
    public static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "resource-mode.json");

    private readonly string _filePath;

    public ResourceModeStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath;
    }

    /// <summary>
    /// Lee la preferencia. Si el archivo no existe, está dañado o no se puede
    /// leer, devuelve <c>false</c>: el modo estricto es opt-in.
    /// </summary>
    public bool Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return false;

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<ResourceModeSettings>(json);
            return settings?.UseOnlyResources ?? false;
        }
        catch
        {
            // Una preferencia ilegible no debe impedir usar la app.
            return false;
        }
    }

    /// <summary>
    /// Guarda la preferencia de forma atómica. Es best-effort: un fallo de
    /// disco nunca debe romper el chat.
    /// </summary>
    public void Save(bool useOnlyResources)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                new ResourceModeSettings { UseOnlyResources = useOnlyResources });

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            // Persistencia best-effort.
        }
    }

    private sealed class ResourceModeSettings
    {
        public bool UseOnlyResources { get; set; }
    }
}
