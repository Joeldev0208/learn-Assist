using System.Collections.Generic;

namespace learn_Assist.Services;

/// <summary>
/// Resultado del contexto construido por <see cref="IContextBuilder"/>.
/// Contiene el system prompt completo listo para enviar a la IA, más
/// metadatos sobre citas permitidas y unidades del programa.
/// </summary>
public sealed record BuiltContext(
    string SystemPrompt,
    IReadOnlyList<string> AllowedCitations,
    IReadOnlyList<string> ProgramUnits);
