using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace learn_Assist.Services;

/// <summary>
/// Servicio que provee el rules-file de conducta del perfil docente
/// (conductor de aprendizaje) para el asistente de IA.
/// </summary>
public enum LearnerLevel
{
    Student,
    University,
}

/// <summary>
/// Contrato para obtener el perfil docente (rules-file de conducta)
/// según el nivel del aprendiz. No contiene hechos del programa;
/// solo las reglas de cómo enseñar.
/// </summary>
public interface ITeacherProfileService
{
    /// <summary>
    /// Obtiene el contenido del rules-file para el nivel dado.
    /// </summary>
    Task<string> GetAsync(LearnerLevel level, CancellationToken ct = default);

    /// <summary>
    /// Obtiene la versión actual del rules-file (hash del contenido)
    /// para detectar divergencias entre conducta y programa.
    /// </summary>
    string GetProfileVersion(LearnerLevel level);
}
