namespace learn_Assist.Services;

/// <summary>
/// Origen de un bloque de contexto inyectado al system prompt de la IA.
/// </summary>
public enum ContextOrigin
{
    TeacherProfile,
    Program,
    Material,
}

/// <summary>
/// Un bloque de contexto con su origen, para que el constructor de prompts
/// sepa qué es conducta, qué es programa del usuario y qué es material de consulta.
/// </summary>
public sealed record ContextBlock(string Content, ContextOrigin Origin);
