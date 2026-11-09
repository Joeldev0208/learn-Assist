using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Implementación de <see cref="IContextBuilder"/> que combina bloques de contexto
/// en un system prompt único, con el perfil docente primero para que no se diluya.
/// </summary>
public sealed class ProgramContextBuilder : IContextBuilder
{
    private static readonly ProgramContextBuilder _instance = new();
    public static ProgramContextBuilder Instance => _instance;

    /// <summary>
    /// Presupuesto total de caracteres para el material importado (~90k tokens).
    /// Sin este tope, varios libros juntos producen un system prompt que el modelo
    /// rechaza por exceder su ventana de contexto.
    /// </summary>
    private const int MaxMaterialCharacters = 360_000;

    /// <summary>
    /// Si queda menos presupuesto que esto, no vale la pena incluir un fragmento.
    /// </summary>
    private const int MinUsefulSlice = 2_000;

    /// <summary>
    /// Restricción del modo «Solo recursos»: el material importado es la única
    /// fuente de hechos. Se coloca al final para que el modelo la lea al último.
    /// </summary>
    private const string ResourcesOnlyRules = """
        ## MODO SOLO RECURSOS (prioridad sobre las reglas anteriores)

        En este modo no hay programa del semestre ni fuentes externas.
        Tu única fuente de hechos es la sección MATERIAL de este mensaje.
        - Responde exclusivamente con información que aparezca en ese material.
        - Ignora la regla de «anclar al programa»: aquí el MATERIAL sustituye al programa.
        - No uses conocimiento general ni el perfil docente como fuente de datos.
        - Cita siempre el documento de origen con el formato `[nombre del documento]`, usando el nombre que aparece en su encabezado `###`.
        - Si la respuesta no está en el material, dilo explícitamente: «Eso no aparece en los recursos seleccionados». Después enumera los recursos disponibles y pide que se marque o importe el recurso adecuado.
        - No inventes datos, fechas, fórmulas, cifras ni referencias que no estén en el material.
        - Si el material está en otro idioma, responde en el idioma del estudiante, sin traducir nombres propios, identificadores ni fórmulas.
        """;

    /// <summary>
    /// Variante del modo estricto cuando no hay ningún recurso marcado.
    /// </summary>
    private const string NoResourcesWarning = """
        ## MODO SOLO RECURSOS (prioridad sobre las reglas anteriores)

        No hay recursos seleccionados en este momento.
        No puedes responder con conocimiento general ni con el programa del semestre.
        Responde al estudiante que marque al menos un recurso en el panel «Resources» antes de continuar.
        """;

    private ProgramContextBuilder()
    {
    }

    /// <summary>
    /// Construye el system prompt compuesto.
    /// Orden de bloques:
    ///   1. AiPrompt.System (conducta base)
    ///   2. PERFIL DOCENTE (rules-file, primero para que no se diluya)
    ///   3. PROGRAMA DEL USUARIO (SemesterContent)
    ///   4. MATERIAL (documentos importados)
    ///   5. RESTRICCIÓN «Solo recursos» (si está activa)
    /// </summary>
    public async Task<BuiltContext> BuildAsync(
        LearnerLevel level,
        string? programContent,
        IReadOnlyList<UserDocument> materials,
        CancellationToken ct = default,
        bool resourcesOnly = false)
    {
        ct.ThrowIfCancellationRequested();

        var blocks = new List<ContextBlock>();
        var citations = new List<string>();
        var programUnits = new List<string>();

        // --- Bloque 1: Conducta base (AiPrompt.System) ---
        var baseSystem = AiPrompt.System;
        if (!string.IsNullOrWhiteSpace(baseSystem))
        {
            blocks.Add(new ContextBlock(baseSystem, ContextOrigin.TeacherProfile));
        }

        // --- Bloque 2: Perfil docente (rules-file) ---
        var teacherProfile = await TeacherProfileService.Instance.GetAsync(level, ct);
        if (!string.IsNullOrWhiteSpace(teacherProfile))
        {
            blocks.Add(new ContextBlock(teacherProfile, ContextOrigin.TeacherProfile));
            // Extraer citas mencionadas en el rules-file (ej. [Unidad 3])
            ExtractCitations(teacherProfile, citations);
        }

        // --- Bloque 3: Programa del usuario (SemesterContent) ---
        // En modo «Solo recursos» se omite a propósito: la única fuente de
        // hechos debe ser el material importado, no el pensum.
        if (!resourcesOnly && !string.IsNullOrWhiteSpace(programContent))
        {
            blocks.Add(new ContextBlock(programContent, ContextOrigin.Program));
            ExtractProgramUnits(programContent, programUnits);
            // Las unidades del programa también son citas permitidas
            foreach (var unit in programUnits)
                if (!citations.Contains(unit))
                    citations.Add(unit);
        }

        // --- Bloque 4: Material (documentos importados) ---
        if (materials is not null && materials.Count > 0)
        {
            var materialParts = new List<string>();
            var omitted = new List<string>();
            var budget = MaxMaterialCharacters;

            foreach (var doc in materials)
            {
                if (string.IsNullOrWhiteSpace(doc.Content))
                    continue;

                var content = doc.Content;

                if (content.Length > budget)
                {
                    if (budget < MinUsefulSlice)
                    {
                        omitted.Add(doc.Name);
                        continue;
                    }

                    content = string.Concat(
                        content.AsSpan(0, budget),
                        "\n\n[... resto del documento omitido por límite de contexto ...]");
                    budget = 0;
                }
                else
                {
                    budget -= content.Length;
                }

                materialParts.Add($"### {doc.Name}\n{content}");
                if (!string.IsNullOrWhiteSpace(doc.Name))
                    citations.Add(doc.Name);
            }

            if (omitted.Count > 0)
            {
                materialParts.Add(
                    "### Documentos no incluidos por límite de contexto\n"
                    + string.Join("\n", omitted.Select(name => $"- {name}")));
            }

            if (materialParts.Count > 0)
            {
                var materialText = string.Join("\n\n---\n\n", materialParts);
                blocks.Add(new ContextBlock(materialText, ContextOrigin.Material));
            }
        }

        // --- Bloque 5: Restricción «Solo recursos» ---
        if (resourcesOnly)
        {
            var hasMaterial = blocks.Any(block => block.Origin == ContextOrigin.Material);
            blocks.Add(new ContextBlock(
                hasMaterial ? ResourcesOnlyRules : NoResourcesWarning,
                ContextOrigin.Restriction));
        }

        // --- Ensamblar system prompt ---
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            if (sb.Length > 0)
                sb.Append("\n\n---\n\n");
            sb.Append(block.Content);
        }

        return new BuiltContext(
            sb.ToString(),
            citations.AsReadOnly(),
            programUnits.AsReadOnly());
    }

    private static void ExtractCitations(string content, List<string> citations)
    {
        // Buscar citas con formato [Unidad X] o [tema]
        var idx = 0;
        while ((idx = content.IndexOf('[', idx)) >= 0)
        {
            var endIdx = content.IndexOf(']', idx);
            if (endIdx < 0)
                break;

            var citation = content.Substring(idx, endIdx - idx + 1);
            if (!citations.Contains(citation))
                citations.Add(citation);

            idx = endIdx + 1;
        }
    }

    private static void ExtractProgramUnits(string programContent, List<string> units)
    {
        // Buscar líneas que contengan "Unidad" para extraer unidades del programa
        var lines = programContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Unidad", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Unit ", StringComparison.OrdinalIgnoreCase))
            {
                var unitName = trimmed.Length > 50 ? trimmed.Substring(0, 50) : trimmed;
                if (!units.Contains(unitName))
                    units.Add(unitName);
            }
        }
    }
}
