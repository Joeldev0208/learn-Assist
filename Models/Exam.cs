using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

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
/// Pregunta individual de un examen, con estado de respuesta del usuario.
/// </summary>
public partial class ExamQuestion : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Prompt { get; set; } = string.Empty;        // enunciado
    public QuestionType Type { get; set; }
    public string? CorrectAnswer { get; set; }               // null en modo práctica
    public string? Citation { get; set; }                    // [Unidad 3] / [Documento: x.pdf]
    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = [];          // solo multiple choice

    // --- Estado de respuesta del usuario ---
    [ObservableProperty]
    private int _selectedOptionIndex = -1;                   // índice de la opción elegida (-1 = ninguna)

    [ObservableProperty]
    private string? _userAnswer;                             // respuesta escrita (ShortAnswer / Essay)

    /// <summary>
    /// Indica si la opción seleccionada coincide con la respuesta correcta.
    /// Solo aplica a MultipleChoice y cuando se conoce la respuesta correcta.
    /// </summary>
    public bool IsCorrect =>
        SelectedOptionIndex >= 0
        && Options.Count > SelectedOptionIndex
        && !string.IsNullOrEmpty(CorrectAnswer)
        && string.Equals(Options[SelectedOptionIndex], CorrectAnswer, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indica si el usuario respondió pero la selección es incorrecta.
    /// Útil para mostrar feedback visual.
    /// </summary>
    public bool IsIncorrect =>
        SelectedOptionIndex >= 0
        && !IsCorrect
        && !string.IsNullOrEmpty(CorrectAnswer);

    /// <summary>
    /// Indica si la pregunta ha sido respondida por el usuario.
    /// </summary>
    public bool IsAnswered =>
        Type == QuestionType.MultipleChoice
            ? SelectedOptionIndex >= 0
            : !string.IsNullOrWhiteSpace(UserAnswer);

    partial void OnSelectedOptionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCorrect));
        OnPropertyChanged(nameof(IsIncorrect));
        OnPropertyChanged(nameof(IsAnswered));
    }

    partial void OnUserAnswerChanged(string? value)
    {
        OnPropertyChanged(nameof(IsAnswered));
    }
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
