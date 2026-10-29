using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

/// <summary>
/// Pruebas unitarias para la feature "Creación de Exámenes" (<see cref="ExamService"/>).
/// Cubre la construcción del system prompt (CA-8), el parseo de respuestas de la IA (CA-6)
/// y la persistencia en disco de exámenes (CA-7).
/// </summary>
public class ExamTests : IDisposable
{
    private readonly List<string> _examIdsToDelete = [];

    private static string ExamsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "exams");

    public void Dispose()
    {
        foreach (var examId in _examIdsToDelete)
        {
            try
            {
                var path = Path.Combine(ExamsDirectory, $"{examId}.json");
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Los errores de limpieza no deben hacer fallar la prueba.
            }
        }
    }

    /// <summary>
    /// Implementación de <see cref="IAiService"/> que captura el historial enviado
    /// a <see cref="IAiService.AskAsync"/> y devuelve una respuesta predefinida.
    /// </summary>
    private sealed class CapturingAiService : IAiService
    {
        public List<ChatMessage> CapturedHistory { get; private set; } = [];
        public string LastMessage { get; private set; } = string.Empty;
        public int CallCount { get; private set; }
        public string Response { get; set; } = string.Empty;

        public void Dispose() { }

        public Task<string> AskAsync(string message, List<ChatMessage> history)
        {
            LastMessage = message ?? string.Empty;
            CapturedHistory = history ?? [];
            CallCount++;
            return Task.FromResult(Response);
        }
    }

    private static ExamService NewService(CapturingAiService mock) => new(mock);

    private static UserDocument Doc(string name, string content) =>
        new() { Name = name, Content = content };

    private static string SystemPrompt(CapturingAiService mock) =>
        mock.CapturedHistory.First(m => m.Role == MessageRole.System).Content;

    // ---------------------------------------------------------------
    // CA-8: system prompt (único MessageRole.System, ancla y citas)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GenerateExamAsync_BuildsSystemPromptWithSingleSystemMessage()
    {
        var mock = new CapturingAiService
        {
            Response = """
{
  "title": "Examen",
  "questions": []
}
"""
        };
        var service = NewService(mock);
        var docs = new List<UserDocument> { Doc("apuntes.pdf", "Contenido de apuntes.") };

        _ = await service.GenerateExamAsync(
            LearnerLevel.Student,
            "Unidad 1: Introducción\nContenido del programa.",
            docs,
            3,
            QuestionType.MultipleChoice,
            includeSolutions: true);

        // Debe enviarse exactamente un system message y un user message (no dos system).
        Assert.Equal(1, mock.CapturedHistory.Count(m => m.Role == MessageRole.System));
        Assert.Equal(1, mock.CapturedHistory.Count(m => m.Role == MessageRole.User));
        Assert.Equal(2, mock.CapturedHistory.Count);

        // El bloque de instrucciones de examen se adjunta al mismo system prompt.
        Assert.Contains("### INSTRUCCIONES PARA GENERAR EL EXAMEN", SystemPrompt(mock));
    }

    [Fact]
    public async Task GenerateExamAsync_PromptContainsProgramAnchor()
    {
        const string programContent = "Unidad 1: Introducción a C#\nVariables, tipos y expresiones.\nUnidad 2: Programación orientada a objetos.";
        var mock = new CapturingAiService
        {
            Response = """
{
  "title": "Examen",
  "questions": []
}
"""
        };
        var service = NewService(mock);

        await service.GenerateExamAsync(
            LearnerLevel.Student,
            programContent,
            [],
            3,
            QuestionType.MultipleChoice,
            includeSolutions: true);

        var prompt = SystemPrompt(mock);
        // El SemesterContent (ancla al programa) debe estar presente en el system prompt.
        Assert.Contains(programContent, prompt);
        Assert.Contains("Unidad 1: Introducción a C#", prompt);
        Assert.Contains("Unidad 2: Programación orientada a objetos.", prompt);
    }

    [Fact]
    public async Task GenerateExamAsync_PromptContainsCitations()
    {
        var docs = new List<UserDocument>
        {
            Doc("apuntes.pdf", "El ciclo de vida del software incluye requisitos, diseño y prueba."),
            Doc("guia.docx", "Control de versiones con Git y ramas.")
        };
        var mock = new CapturingAiService
        {
            Response = """
{
  "title": "Examen",
  "questions": []
}
"""
        };
        var service = NewService(mock);

        await service.GenerateExamAsync(
            LearnerLevel.Student,
            "programa base",
            docs,
            3,
            QuestionType.MultipleChoice,
            includeSolutions: true);

        var prompt = SystemPrompt(mock);
        // El system prompt instruye a citar documentos con el formato [Documento: ...].
        Assert.Contains("[Documento: ", prompt);
        // Los nombres de los documentos importados aparecen en el system prompt.
        Assert.Contains("apuntes.pdf", prompt);
        Assert.Contains("guia.docx", prompt);
    }

    // ---------------------------------------------------------------
    // CA-6: parseo de la respuesta de la IA
    // ---------------------------------------------------------------

    [Fact]
    public async Task ParseExamResponse_ValidJson_ReturnsExamWithQuestions()
    {
        var mock = new CapturingAiService
        {
            Response = """
{
  "title": "Examen de C#",
  "questions": [
    {
      "prompt": "¿Qué es una variable?",
      "type": "MultipleChoice",
      "correctAnswer": "Un contenedor de datos",
      "citation": "[Unidad 1]",
      "options": ["Un contenedor de datos", "Un bucle", "Una clase", "Un método"]
    },
    {
      "prompt": "¿Qué es POO?",
      "type": "MultipleChoice",
      "correctAnswer": "Programación orientada a objetos",
      "citation": "[Documento: poo.pdf]",
      "options": ["Programación orientada a objetos", "Un lenguaje de bajo nivel", "Un sistema operativo"]
    }
  ]
}
"""
        };
        var service = NewService(mock);

        var exam = await service.GenerateExamAsync(
            LearnerLevel.Student,
            "Unidad 1: Introducción a C#",
            [Doc("poo.pdf", "Contenido de POO.")],
            2,
            QuestionType.MultipleChoice,
            includeSolutions: true);

        Assert.NotNull(exam);
        Assert.Equal("Examen de C#", exam.Title);
        Assert.Equal(2, exam.Questions.Count);

        var q1 = exam.Questions[0];
        Assert.Equal("¿Qué es una variable?", q1.Prompt);
        Assert.Equal(QuestionType.MultipleChoice, q1.Type);
        Assert.Equal("Un contenedor de datos", q1.CorrectAnswer);
        Assert.Equal("[Unidad 1]", q1.Citation);
        Assert.Equal(4, q1.Options.Count);
        Assert.Contains("Un contenedor de datos", q1.Options);

        var q2 = exam.Questions[1];
        Assert.Equal("¿Qué es POO?", q2.Prompt);
        Assert.Equal("Programación orientada a objetos", q2.CorrectAnswer);
        Assert.Equal("[Documento: poo.pdf]", q2.Citation);
        Assert.Equal(3, q2.Options.Count);
    }

    [Fact]
    public async Task ParseExamResponse_PracticeMode_OmitsCorrectAnswer()
    {
        var mock = new CapturingAiService
        {
            Response = """
{
  "title": "Examen práctica",
  "questions": [
    { "prompt": "¿Qué es una variable?", "type": "ShortAnswer", "citation": "[Unidad 1]" },
    { "prompt": "¿Qué es POO?", "type": "ShortAnswer", "citation": "[Unidad 2]" }
  ]
}
"""
        };
        var service = NewService(mock);

        var exam = await service.GenerateExamAsync(
            LearnerLevel.Student,
            "programa",
            [],
            2,
            QuestionType.ShortAnswer,
            includeSolutions: false);

        Assert.Equal(2, exam.Questions.Count);
        // En modo práctica no debe incluirse la solución de ninguna pregunta.
        Assert.All(exam.Questions, q => Assert.Null(q.CorrectAnswer));
        Assert.All(exam.Questions, q => Assert.Equal(QuestionType.ShortAnswer, q.Type));
    }

    [Fact]
    public async Task ParseExamResponse_InvalidJson_FallbackLines()
    {
        var mock = new CapturingAiService
        {
            Response = """
1. ¿Qué es una variable?
2. ¿Qué es POO?
3. Explica la herencia.
Aquí hay texto extra que no es JSON.
"""
        };
        var service = NewService(mock);

        var exam = await service.GenerateExamAsync(
            LearnerLevel.Student,
            "programa",
            [],
            3,
            QuestionType.Essay,
            includeSolutions: false);

        Assert.Equal(3, exam.Questions.Count);
        Assert.Equal("¿Qué es una variable?", exam.Questions[0].Prompt);
        Assert.Equal("¿Qué es POO?", exam.Questions[1].Prompt);
        Assert.Equal("Explica la herencia.", exam.Questions[2].Prompt);
        Assert.All(exam.Questions, q => Assert.Null(q.CorrectAnswer));
        Assert.All(exam.Questions, q => Assert.Equal(QuestionType.Essay, q.Type));
    }

    // ---------------------------------------------------------------
    // CA-7: persistencia
    // ---------------------------------------------------------------

    [Fact]
    public async Task SaveAndLoadExam_RoundTrip()
    {
        var service = NewService(new CapturingAiService { Response = "{ \"title\": \"x\", \"questions\": [] }" });
        var exam = new Exam
        {
            Title = "Examen de prueba",
            UserId = "usuario-1",
            SourceProgram = "Unidad 1: Introducción",
            SourceDocuments = ["apuntes.pdf"],
            Questions =
            {
                new ExamQuestion
                {
                    Prompt = "¿Qué es una variable?",
                    Type = QuestionType.MultipleChoice,
                    CorrectAnswer = "Un contenedor de datos",
                    Citation = "[Unidad 1]",
                    Options = ["Un contenedor de datos", "Un bucle"]
                },
                new ExamQuestion
                {
                    Prompt = "Explica POO",
                    Type = QuestionType.ShortAnswer,
                    CorrectAnswer = null,
                    Citation = "[Unidad 2]",
                    Options = []
                }
            }
        };

        await service.SaveExamAsync(exam);
        _examIdsToDelete.Add(exam.Id);

        var loaded = await service.LoadExamAsync(exam.Id);

        Assert.NotNull(loaded);
        Assert.Equal(exam.Id, loaded.Id);
        Assert.Equal(exam.Title, loaded.Title);
        Assert.Equal(exam.UserId, loaded.UserId);
        Assert.Equal(exam.SourceProgram, loaded.SourceProgram);
        Assert.Equal(exam.SourceDocuments, loaded.SourceDocuments);
        Assert.Equal(exam.Questions.Count, loaded.Questions.Count);
        Assert.Equal(exam.Questions[0].Prompt, loaded.Questions[0].Prompt);
        Assert.Equal(exam.Questions[0].Type, loaded.Questions[0].Type);
        Assert.Equal(exam.Questions[0].CorrectAnswer, loaded.Questions[0].CorrectAnswer);
        Assert.Equal(exam.Questions[0].Citation, loaded.Questions[0].Citation);
        Assert.Equal(exam.Questions[0].Options, loaded.Questions[0].Options);
        Assert.Equal(exam.Questions[1].CorrectAnswer, loaded.Questions[1].CorrectAnswer);
    }

    [Fact]
    public async Task SaveExam_AtomicWrite_CreatesFileInExamsDirectory()
    {
        var service = NewService(new CapturingAiService { Response = "{ \"title\": \"x\", \"questions\": [] }" });
        var exam = new Exam
        {
            Title = "Examen persistencia",
            UserId = "usuario-1",
            Questions = { new ExamQuestion { Prompt = "P1", Type = QuestionType.ShortAnswer } }
        };

        await service.SaveExamAsync(exam);
        _examIdsToDelete.Add(exam.Id);

        var path = Path.Combine(ExamsDirectory, $"{exam.Id}.json");
        Assert.True(File.Exists(path), $"El archivo JSON no se creó en {path}");

        var json = await File.ReadAllTextAsync(path);
        Assert.NotEmpty(json);
        Assert.Contains(exam.Id, json);
        Assert.Contains("Examen persistencia", json);
    }
}
