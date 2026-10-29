using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace learn_Assist.Models;

/// <summary>
/// Tipo de pregunta de un examen generado.
/// </summary>
public enum QuestionType
{
    MultipleChoice,
    ShortAnswer,
    Essay,
}

/// <summary>
/// Pregunta individual de un examen.
/// </summary>
public sealed class ExamQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Prompt { get; set; } = string.Empty;        // enunciado
    public QuestionType Type { get; set; }
    public string? CorrectAnswer { get; set; }               // null en modo práctica
    public string? Citation { get; set; }                    // [Unidad 3] / [Documento: x.pdf]
    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = [];          // solo multiple choice
}

/// <summary>
/// Examen generado por la IA, persistible y recuperable.
/// </summary>
public sealed class Exam
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<ExamQuestion> Questions { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? SourceProgram { get; set; }               // ref a SemesterContent
    public List<string> SourceDocuments { get; set; } = [];
}
