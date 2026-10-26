using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Construye el system prompt compuesto para la IA, combinando:
/// 1. Conducta (AiPrompt.System + TeacherProfile rules-file, primero para que no se diluya)
/// 2. Programa del usuario (SemesterContent)
/// 3. Material (documentos importados como contexto)
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Construye el system prompt completo y los metadatos de contexto.
    /// </summary>
    Task<BuiltContext> BuildAsync(
        LearnerLevel level,
        string? programContent,
        IReadOnlyList<UserDocument> materials,
        CancellationToken ct = default);
}
