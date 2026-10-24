using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests.Eval;

/// <summary>
/// Harness de evaluación del Perfil Docente (conductor de aprendizaje).
///
/// Este harness verifica que el system prompt construido por
/// <see cref="ProgramContextBuilder"/> incluya las reglas de conducta
/// definidas en los rules-files y que el contenido del programa se inyecte
/// correctamente cuando está disponible.
///
/// No es un harness de aprendizaje: es una herramienta de desarrollo para
/// validar métricas antes de desplegar cambios en el perfil docente.
/// </summary>
public class EvalHarnessTests
{
    private const string QuestionsPath = "Eval/questions.json";
    private const string ProgramPath = "Eval/programs/syllabus_csharp_ejemplo.txt";

    private readonly string _repoRoot;
    private readonly List<EvalQuestion> _questions;
    private readonly string _syllabusContent;

    public EvalHarnessTests()
    {
        _repoRoot = FindRepoRoot();
        _questions = LoadQuestions();
        _syllabusContent = LoadSyllabus();
    }

    #region Métrica 1: Ancla al programa (≥90%)

    [Fact]
    public async Task Metrica_AnclaAlPrograma_PreguntasDentroDelPrograma_IncluyenContexto()
    {
        var dentroDelPrograma = _questions.Where(q => !q.EsFueraDePrograma).ToList();
        var total = dentroDelPrograma.Count;
        var conContexto = 0;

        foreach (var q in dentroDelPrograma)
        {
            var context = await ConstruirContextoAsync(q);
            if (ContieneContextoDelPrograma(context.SystemPrompt, _syllabusContent))
                conContexto++;
        }

        var porcentaje = (double)conContexto / total * 100;
        Assert.True(porcentaje >= 90, $"Ancla al programa: {porcentaje:F1}% (umbral ≥90%)");
    }

    [Fact]
    public async Task Metrica_AnclaAlPrograma_PreguntasFueraDelPrograma_IncluyenReglaDeAncla()
    {
        var fueraDelPrograma = _questions.Where(q => q.EsFueraDePrograma).ToList();
        var total = fueraDelPrograma.Count;
        var conReglaDeAncla = 0;

        foreach (var q in fueraDelPrograma)
        {
            var context = await ConstruirContextoAsync(q);
            // El rules-file debe incluir instrucción de ancla, por lo que el system prompt
            // debe contener la regla aunque el tema no esté en el programa
            if (ContieneReglaDeAncla(context.SystemPrompt))
                conReglaDeAncla++;
        }

        var porcentaje = (double)conReglaDeAncla / total * 100;
        Assert.True(porcentaje >= 90, $"Ancla al programa (fuera): {porcentaje:F1}% (umbral ≥90%)");
    }

    #endregion

    #region Métrica 2: Cita verificable (≥95%)

    [Fact]
    public async Task Metrica_CitaVerificable_PreguntasTipoCita_IncluyenInstruccionDeCita()
    {
        var tipoCita = _questions.Where(q => q.Tipo == "cita").ToList();
        var total = tipoCita.Count;
        var conInstruccion = 0;

        foreach (var q in tipoCita)
        {
            var context = await ConstruirContextoAsync(q);
            if (ContieneInstruccionDeCita(context.SystemPrompt))
                conInstruccion++;
        }

        var porcentaje = (double)conInstruccion / total * 100;
        Assert.True(porcentaje >= 95, $"Cita verificable: {porcentaje:F1}% (umbral ≥95%)");
    }

    #endregion

    #region Métrica 3: Verifica comprensión (≥80%)

    [Fact]
    public async Task Metrica_VerificaComprension_PreguntasTipoComprension_IncluyenInstruccion()
    {
        var tipoComprension = _questions.Where(q => q.Tipo == "comprension").ToList();
        var total = tipoComprension.Count;
        var conInstruccion = 0;

        foreach (var q in tipoComprension)
        {
            var context = await ConstruirContextoAsync(q);
            if (ContieneInstruccionDeVerificacion(context.SystemPrompt))
                conInstruccion++;
        }

        var porcentaje = (double)conInstruccion / total * 100;
        Assert.True(porcentaje >= 80, $"Verifica comprensión: {porcentaje:F1}% (umbral ≥80%)");
    }

    #endregion

    #region Métrica 4: No regalar la respuesta (manual, verificación de instrucción)

    [Fact]
    public async Task Metrica_NoRegalarRespuesta_PreguntasTipoNoRegalar_IncluyenInstruccion()
    {
        var tipoNoRegalar = _questions.Where(q => q.Tipo == "no_regalar_respuesta").ToList();
        var total = tipoNoRegalar.Count;
        var conInstruccion = 0;

        foreach (var q in tipoNoRegalar)
        {
            var context = await ConstruirContextoAsync(q);
            if (ContieneInstruccionNoRegalarRespuesta(context.SystemPrompt))
                conInstruccion++;
        }

        // Métrica manual: revisar que el rules-file incluya la regla
        Assert.True(total > 0, "Debe haber al menos una pregunta de tipo no_regalar_respuesta");
        var porcentaje = (double)conInstruccion / total * 100;
        Assert.True(porcentaje >= 90, $"No regalar respuesta: {porcentaje:F1}% (umbral sugerido ≥90%)");
    }

    #endregion

    #region Métrica 5: Divergencia rules↔programa (el rules-file no contiene hechos del programa)

    [Fact]
    public void Metrica_DivergenciaRulesVsPrograma_NoContieneHechosDelPrograma()
    {
        // El rules-file no debe contener hechos del programa (unidades, temas, fechas)
        // que contradigan el syllabus importado
        var hechosDelPrograma = new[]
        {
            "Unidad 1: Introducción a C#",
            "Unidad 2: Programación orientada a objetos",
            "semestre 2026-1",
        };

        var studentContent = ObtenerContenidoRulesFile(LearnerLevel.Student).Result;
        var universityContent = ObtenerContenidoRulesFile(LearnerLevel.University).Result;

        foreach (var hecho in hechosDelPrograma)
        {
            Assert.DoesNotContain(hecho, studentContent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hecho, universityContent, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region Helpers

    private async Task<BuiltContext> ConstruirContextoAsync(EvalQuestion question)
    {
        return await ConstruirContextoAsync(question, LearnerLevel.Student);
    }

    private async Task<BuiltContext> ConstruirContextoAsync(EvalQuestion question, LearnerLevel level)
    {
        var materials = new List<UserDocument>
        {
            new UserDocument
            {
                Name = "syllabus_csharp_ejemplo.txt",
                Content = _syllabusContent,
                ContentType = DocumentContentType.Document,
            },
        };

        return await ProgramContextBuilder.Instance.BuildAsync(
            level,
            _syllabusContent,
            materials);
    }

    private async Task<string> ObtenerContenidoRulesFile(LearnerLevel level)
    {
        var context = await ProgramContextBuilder.Instance.BuildAsync(level, null, new List<UserDocument>());
        return context.SystemPrompt;
    }

    private static bool ContieneContextoDelPrograma(string systemPrompt, string syllabusContent)
    {
        // El system prompt debe incluir al menos una parte del programa
        var primeraLinea = syllabusContent.Split('\n')
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return !string.IsNullOrEmpty(primeraLinea)
            && systemPrompt.Contains(primeraLinea.Trim());
    }

    private static bool ContieneReglaDeAncla(string systemPrompt)
    {
        return systemPrompt.Contains("Ancla al programa", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("programa del curso importado", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContieneInstruccionDeCita(string systemPrompt)
    {
        return systemPrompt.Contains("Cita la fuente", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("[Unidad", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContieneInstruccionDeVerificacion(string systemPrompt)
    {
        return systemPrompt.Contains("Verifica comprensión", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("comprensión antes de avanzar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContieneInstruccionNoRegalarRespuesta(string systemPrompt)
    {
        return systemPrompt.Contains("No regalar la respuesta", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("NO resuelvas", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "learn-Assist.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private List<EvalQuestion> LoadQuestions()
    {
        var path = Path.Combine(_repoRoot, QuestionsPath);
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        return JsonSerializer.Deserialize<List<EvalQuestion>>(json, options) ?? [];
    }

    private string LoadSyllabus()
    {
        var path = Path.Combine(_repoRoot, ProgramPath);
        return File.ReadAllText(path);
    }

    #endregion
}

/// <summary>
/// Modelo para una pregunta del harness de evaluación.
/// </summary>
public sealed record EvalQuestion(
    int Id,
    string Pregunta,
    string Tipo,
    bool EsFueraDePrograma,
    string RespuestaEsperadaTipo);
