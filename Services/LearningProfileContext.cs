using System.Collections.Generic;
using System.Linq;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Builds the bounded, human-readable summary of a learner's
/// <see cref="LearningProfile"/> that is injected into the AI prompt as a
/// system message. Empty fields are skipped so the message stays short and
/// never grows unbounded.
/// </summary>
internal static class LearningProfileContext
{
    private const int MaxSubjects = 6;

    public static string? Build(LearningProfile? profile)
    {
        if (profile is null)
            return null;

        var lines = new List<string>();

        var program = DescribeProgram(profile);
        if (!string.IsNullOrWhiteSpace(program))
            lines.Add($"- {program}");

        AddIfPresent(lines, "- Curso", profile.Course);
        AddIfPresent(lines, "- Nivel", profile.Level);
        AddIfPresent(lines, "- Área profesional", profile.ProfessionalArea);
        AddSubjects(lines, profile.Subjects);
        AddIfPresent(lines, "- Meta de aprendizaje", profile.LearningGoal);
        AddIfPresent(lines, "- Idioma preferido", profile.PreferredLanguage);

        if (lines.Count == 0)
            return null;

        return "Contexto del estudiante (perfil de aprendizaje):\n" + string.Join("\n", lines);
    }

    private static string? DescribeProgram(LearningProfile profile)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.Career))
            parts.Add(profile.Career.Trim());

        if (!string.IsNullOrWhiteSpace(profile.Semester))
            parts.Add($"semestre {profile.Semester.Trim()}");

        if (!string.IsNullOrWhiteSpace(profile.University))
            parts.Add($"en {profile.University.Trim()}");

        if (parts.Count == 0)
            return null;

        var type = profile.ProfileType switch
        {
            LearningProfileType.Student => "Estudiante de secundaria",
            LearningProfileType.University => "Estudiante universitario",
            LearningProfileType.Professional => "Profesional",
            _ => "Perfil personal",
        };

        return $"{type}: {string.Join(", ", parts)}";
    }

    private static void AddSubjects(List<string> lines, List<string>? subjects)
    {
        if (subjects is null)
            return;

        var cleaned = subjects
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (cleaned.Count == 0)
            return;

        var shown = cleaned.Take(MaxSubjects).ToList();
        var summary = string.Join(", ", shown);

        if (cleaned.Count > shown.Count)
            summary += $" y {cleaned.Count - shown.Count} más";

        lines.Add($"- Materias: {summary}");
    }

    private static void AddIfPresent(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"{label}: {value.Trim()}");
    }
}
