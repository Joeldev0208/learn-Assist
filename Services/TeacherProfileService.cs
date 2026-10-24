using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Implementación de <see cref="ITeacherProfileService"/> que lee los
/// rules-files de conducta embebidos como <see cref="EmbeddedResource"/>
/// y los cachea por nivel, invalidando cuando el hash del archivo cambia.
/// </summary>
public sealed class TeacherProfileService : ITeacherProfileService
{
    private static readonly TeacherProfileService _instance = new();
    public static TeacherProfileService Instance => _instance;

    private readonly Dictionary<LearnerLevel, string> _cache = new();
    private readonly Dictionary<LearnerLevel, string> _versions = new();

    private TeacherProfileService()
    {
    }

    /// <summary>
    /// Obtiene el contenido del rules-file para el nivel dado, cacheado.
    /// </summary>
    public async Task<string> GetAsync(LearnerLevel level, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(level, out var cached))
            return cached;

        var content = await LoadEmbeddedResourceAsync(level, ct);
        var version = ComputeVersion(content);

        _cache[level] = content;
        _versions[level] = version;

        return content;
    }

    /// <summary>
    /// Obtiene la versión (hash SHA-256 truncado) del rules-file para el nivel dado.
    /// </summary>
    public string GetProfileVersion(LearnerLevel level)
    {
        return _versions.TryGetValue(level, out var version)
            ? version
            : throw new InvalidOperationException($"El perfil para el nivel {level} no ha sido cargado aún. Llame a GetAsync primero.");
    }

    /// <summary>
    /// Invalida el cache de un nivel para forzar recarga en la próxima llamada.
    /// </summary>
    public void Invalidate(LearnerLevel level)
    {
        _cache.Remove(level);
        _versions.Remove(level);
    }

    private static async Task<string> LoadEmbeddedResourceAsync(LearnerLevel level, CancellationToken ct)
    {
        var resourceName = level switch
        {
            LearnerLevel.Student => "learn_Assist.prompts.teacher-profile.md",
            LearnerLevel.University => "learn_Assist.prompts.teacher-profile.university.md",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Nivel de aprendiz no soportado."),
        };

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            throw new FileNotFoundException(
                $"No se encontró el recurso embebido '{resourceName}' en el ensamblado '{assembly.GetName().Name}'.",
                resourceName);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static string ComputeVersion(string content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).AsSpan(0, 16).ToString(); // Primeros 16 caracteres hex
    }
}
