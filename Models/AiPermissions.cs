namespace learn_Assist.Models;

/// <summary>
/// Permisos que el usuario concede a la IA. Se guardan aparte de la
/// configuración del proveedor para que un cambio de API no los borre.
///
/// El valor por defecto es restrictivo: la IA solo ve los recursos que el
/// usuario marcó explícitamente y no puede usar herramientas del agente.
/// </summary>
public sealed class AiPermissions
{
    /// <summary>
    /// Interruptor maestro de los recursos. Si está en <c>false</c>, ningún
    /// documento entra al contexto, sin importar los otros permisos.
    /// </summary>
    public bool AllowResources { get; set; } = true;

    /// <summary>
    /// Permite incluir recursos importados desde una URL.
    /// Solo tiene efecto cuando <see cref="AllowResources"/> es <c>true</c>.
    /// </summary>
    public bool AllowWebResources { get; set; } = true;

    /// <summary>
    /// Permite incluir archivos locales (PDF, DOCX, imágenes, etc.).
    /// Solo tiene efecto cuando <see cref="AllowResources"/> es <c>true</c>.
    /// </summary>
    public bool AllowLocalFiles { get; set; } = true;

    /// <summary>
    /// Permite que el agente local (OpenCode) ejecute herramientas: comandos
    /// de shell, edición de archivos y acceso a la red. Desactivado por
    /// defecto porque el agente corre con los permisos del usuario.
    /// </summary>
    public bool AllowAgentTools { get; set; }

    /// <summary>
    /// Valores por defecto: recursos marcados permitidos, herramientas del
    /// agente bloqueadas.
    /// </summary>
    public static AiPermissions Default => new();

    /// <summary>
    /// Copia independiente. La UI edita una copia y solo la guarda al
    /// confirmar, para que «Cancelar» no deje cambios a medias.
    /// </summary>
    public AiPermissions Clone() => new()
    {
        AllowResources = AllowResources,
        AllowWebResources = AllowWebResources,
        AllowLocalFiles = AllowLocalFiles,
        AllowAgentTools = AllowAgentTools,
    };
}
