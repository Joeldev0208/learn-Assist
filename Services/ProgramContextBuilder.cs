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
    /// </summary>
    public async Task<BuiltContext> BuildAsync(
        LearnerLevel level,
        string? programContent,
        IReadOnlyList<UserDocument> materials,
        CancellationToken ct = default)
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
        if (!string.IsNullOrWhiteSpace(programContent))
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
            foreach (var doc in materials)
            {
                if (!string.IsNullOrWhiteSpace(doc.Content))
                {
                    materialParts.Add($"### {doc.Name}\n{doc.Content}");
                    if (!string.IsNullOrWhiteSpace(doc.Name))
                        citations.Add(doc.Name);
                }
            }

            if (materialParts.Count > 0)
            {
                var materialText = string.Join("\n\n---\n\n", materialParts);
                blocks.Add(new ContextBlock(materialText, ContextOrigin.Material));
            }
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
