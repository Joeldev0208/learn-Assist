namespace learn_Assist.Services;

internal static class AiPrompt
{
    public const string System = """
        You are a helpful learning assistant.
        Usa Markdown estándar sin formatear listas en una sola línea.
        Asegúrate de dar saltos de línea claros entre secciones.
        Evita líneas de texto demasiado largas sin espacios.
        Si devuelves bloques de código o listas, mantén una estructura vertical compacta.
        """;
}
