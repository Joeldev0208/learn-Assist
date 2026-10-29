using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Servicio para generar exámenes a partir del programa y los materiales
/// del usuario, usando el servicio de IA configurado, y para persistirlos,
/// cargarlos y listarlos localmente en %APPDATA%/learn-assist/exams/.
/// </summary>
public sealed class ExamService
{
    private static ExamService? _instance;

    /// <summary>
    /// Instancia singleton de conveniencia. Usa <see cref="MockAiService"/>
    /// por defecto (coherente con el comportamiento de la app cuando no hay
    /// proveedor de IA configurado). Para inyectar un proveedor real, usar el
    /// constructor <see cref="ExamService(IAiService)"/>.
    /// </summary>
    public static ExamService Instance => _instance ??= new ExamService(new MockAiService());

    private static readonly string ExamsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "exams");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    static ExamService()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly IAiService _aiService;

    /// <summary>
    /// Crea una nueva instancia con el servicio de IA dado.
    /// </summary>
    /// <exception cref="ArgumentNullException">Si <paramref name="aiService"/> es null.</exception>
    public ExamService(IAiService aiService)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
    }

    /// <summary>
    /// Genera un examen a partir del programa y los materiales usando la IA.
    /// </summary>
    /// <param name="level">Nivel del aprendiz (se usa para el perfil docente del system prompt).</param>
    /// <param name="programContent">Contenido del programa (SemesterContent).</param>
    /// <param name="documents">Materiales importados del usuario.</param>
    /// <param name="questionCount">Cantidad de preguntas a generar.</param>
    /// <param name="questionType">Tipo de pregunta para todas las preguntas del examen.</param>
    /// <param name="includeSolutions">Si es false (modo práctica), se omite la solución de cada pregunta.</param>
    /// <returns>El <see cref="Exam"/> generado con <see cref="Exam.CreatedAtUtc"/>,
    /// <see cref="Exam.SourceProgram"/> y <see cref="Exam.SourceDocuments"/> llenados.</returns>
    public async Task<Exam> GenerateExamAsync(
        LearnerLevel level,
        string programContent,
        IReadOnlyList<UserDocument> documents,
        int questionCount,
        QuestionType questionType,
        bool includeSolutions)
    {
        var count = questionCount < 1 ? 5 : (questionCount > 50 ? 50 : questionCount);
        var docs = documents ?? [];
        var program = programContent ?? string.Empty;

        // System prompt compuesto (UN SOLO MessageRole.System).
        // RNF-E01: ProgramContextBuilder no se toca; emite un único system message.
        var built = await ProgramContextBuilder.Instance.BuildAsync(level, program, docs);

        // Bloque de instrucción de examen añadido al system prompt ya construido
        // (no es un segundo system message, se envía como parte del mismo).
        var composedSystemPrompt = AppendExamInstructions(built.SystemPrompt, count, questionType, includeSolutions);

        // Historial: un System (system prompt compuesto) + un User.
        var history = new List<ChatMessage>
        {
            new ChatMessage { Role = MessageRole.System, Content = composedSystemPrompt },
            new ChatMessage
            {
                Role = MessageRole.User,
                Content = $"Genera un examen de {count} preguntas de tipo {questionType} basado en el programa y los materiales."
            }
        };

        var response = await _aiService.AskAsync("Genera el examen", history);

        return ParseExamResponse(response, count, questionType, program, docs);
    }

    /// <summary>
    /// Añade al final del system prompt el bloque de instrucción de examen.
    /// No crea un segundo MessageRole.System; se entrega como parte del mismo system prompt.
    /// </summary>
    private static string AppendExamInstructions(
        string systemPrompt,
        int count,
        QuestionType questionType,
        bool includeSolutions)
    {
        var sb = new StringBuilder(systemPrompt);
        if (sb.Length > 0 && !sb.ToString().EndsWith("\n"))
            sb.Append('\n');

        sb.Append("\n### INSTRUCCIONES PARA GENERAR EL EXAMEN\n");
        sb.Append($"Crea exactamente {count} preguntas basadas ÚNICAMENTE en el programa y los materiales proporcionados arriba. ");
        sb.AppendLine("No uses conocimientos externos ni inventes información que no aparezca en esas fuentes.");
        sb.Append("Ancla cada pregunta al contenido fuente y cita siempre su origen entre corchetes: ");
        sb.AppendLine("[Unidad X] para el programa o [Documento: nombre] para un material importado.");
        sb.Append($"Todas las preguntas deben ser de tipo {questionType}.\n");
        sb.AppendLine();

        sb.AppendLine("Devuelve ÚNICAMENTE un objeto JSON válido, sin texto antes ni después y sin bloques de código markdown.");
        sb.AppendLine("Estructura esperada:");
        sb.AppendLine("{");
        sb.AppendLine("  \"title\": \"Título del examen\",");
        sb.AppendLine("  \"questions\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"prompt\": \"enunciado de la pregunta\",");
        sb.AppendLine("      \"type\": \"MultipleChoice | ShortAnswer | Essay\",");
        sb.AppendLine("      \"correctAnswer\": \"respuesta correcta (omitir en modo práctica)\",");
        sb.AppendLine("      \"citation\": \"[Unidad 3] o [Documento: nombre.pdf]\",");
        sb.AppendLine("      \"options\": [\"opción A\", \"opción B\", \"opción C\", \"opción D\"]");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();

        if (includeSolutions)
            sb.AppendLine("Incluye la respuesta correcta en cada pregunta.");
        else
            sb.AppendLine("MODO PRÁCTICA: no incluyas la solución; deja correctAnswer en null en cada pregunta.");

        return sb.ToString();
    }

    /// <summary>
    /// Parsea la respuesta de la IA a un <see cref="Exam"/>. Intenta JSON primero
    /// y hace fallback a parseo por líneas numeradas si la IA no devuelve JSON válido.
    /// </summary>
    private static Exam ParseExamResponse(
        string response,
        int count,
        QuestionType questionType,
        string programContent,
        IReadOnlyList<UserDocument> documents)
    {
        var exam = new Exam
        {
            Title = "Examen generado",
            SourceProgram = programContent,
            SourceDocuments = documents
                .Select(d => d.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        var jsonText = ExtractJsonObject(response);
        if (!string.IsNullOrWhiteSpace(jsonText))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Exam>(jsonText, JsonOptions);
                if (parsed is not null)
                {
                    exam.Id = Guid.NewGuid().ToString();
                    exam.CreatedAtUtc = DateTime.UtcNow;
                    exam.Title = string.IsNullOrWhiteSpace(parsed.Title) ? "Examen generado" : parsed.Title!;

                    foreach (var q in parsed.Questions ?? [])
                    {
                        if (q is not null)
                        {
                            q.Id = Guid.NewGuid().ToString();
                            if (q.Options is null)
                                q.Options = [];
                            exam.Questions.Add(q);
                        }
                    }

                    if (exam.Questions.Count > 0)
                        return exam;
                }
            }
            catch
            {
                // El JSON no es válido: se usa el fallback de parseo por líneas.
            }
        }

        // Fallback: parseo por líneas numeradas ("1. enunciado", "1) enunciado", ...).
        ParseByLines(response, exam, questionType);
        return exam;
    }

    /// <summary>
    /// Extrae el primer objeto JSON de un texto (ignora texto alrededor y bloques ```json).
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < 0 || end < start)
            return null;
        return text.Substring(start, end - start + 1);
    }

    /// <summary>
    /// Parseo de fallback por líneas: detecta líneas numeradas ("1.", "1)", "1-", "1:")
    /// y las usa como enunciado de pregunta.
    /// </summary>
    private static void ParseByLines(string response, Exam exam, QuestionType questionType)
    {
        foreach (var rawLine in response.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length < 3)
                continue;

            var digitEnd = 0;
            while (digitEnd < line.Length && char.IsDigit(line[digitEnd]))
                digitEnd++;

            if (digitEnd == 0 || digitEnd >= line.Length)
                continue;

            var separator = line[digitEnd];
            if (separator != '.' && separator != ')' && separator != '-' && separator != ':')
                continue;

            var prompt = line.Substring(digitEnd + 1).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                continue;

            exam.Questions.Add(new ExamQuestion
            {
                Prompt = prompt,
                Type = questionType,
                CorrectAnswer = null,
                Citation = string.Empty,
                Options = [],
            });
        }
    }

    /// <summary>
    /// Serializa el examen a JSON en %APPDATA%/learn-assist/exams/{examId}.json
    /// con escritura atómica (.tmp + File.Move).
    /// </summary>
    public async Task SaveExamAsync(Exam exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (!Directory.Exists(ExamsDirectory))
            Directory.CreateDirectory(ExamsDirectory);

        var path = Path.Combine(ExamsDirectory, $"{exam.Id}.json");
        var json = JsonSerializer.Serialize(exam, JsonOptions);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Carga un examen desde %APPDATA%/learn-assist/exams/{examId}.json.
    /// Devuelve null si no existe o no se puede leer.
    /// </summary>
    public async Task<Exam?> LoadExamAsync(string examId)
    {
        if (string.IsNullOrWhiteSpace(examId))
            return null;

        var path = Path.Combine(ExamsDirectory, $"{examId}.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Exam>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lista los exámenes guardados, ordenados del más reciente al más antiguo.
    /// Los archivos corruptos se ignoran.
    /// </summary>
    public async Task<List<Exam>> ListExamsAsync()
    {
        var result = new List<Exam>();
        if (!Directory.Exists(ExamsDirectory))
            return result;

        foreach (var file in Directory.GetFiles(ExamsDirectory, "*.json")
                     .OrderByDescending(f => File.GetLastWriteTimeUtc(f)))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var exam = JsonSerializer.Deserialize<Exam>(json, JsonOptions);
                if (exam is not null)
                    result.Add(exam);
            }
            catch
            {
                // Ignora archivos corruptos.
            }
        }

        return result;
    }
}
