using System;
using System.Threading.Tasks;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class TeacherProfileTests
{
    [Fact]
    public async Task GetAsync_StudentLevel_ReturnsNonEmptyContent()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetAsync_UniversityLevel_ReturnsNonEmptyContent()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.University);

        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsAnclaAlPrograma()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Ancla al programa", content);
        Assert.Contains("programa del curso importado", content);
        Assert.Contains("Nunca inventes contenido", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsCitaLaFuente()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Cita la fuente", content);
        Assert.Contains("[Unidad X]", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsNoRegalarLaRespuesta()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("No regalar la respuesta", content);
        Assert.Contains("NO resuelvas el ejercicio por él", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsVerificaComprension()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Verifica comprensión antes de avanzar", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsEscalaLaDuda()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Escala la duda", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsConectaConEvaluacion()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Conecta con la evaluación", content);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_ContainsExplicaEnCapas()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        Assert.Contains("Explica en capas", content);
    }

    [Fact]
    public async Task GetAsync_UniversityLevel_ContainsReglasClave()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.University);

        Assert.Contains("Ancla al programa", content);
        Assert.Contains("Cita la fuente académica", content);
        Assert.Contains("No regales la respuesta", content);
        Assert.Contains("Verifica comprensión crítica", content);
        Assert.Contains("Escala la duda y el rigor", content);
        Assert.Contains("Conecta con la evaluación y la investigación", content);
        Assert.Contains("Explica en capas con profundidad académica", content);
    }

    [Fact]
    public void GetProfileVersion_ReturnsNonEmptyHash()
    {
        var service = TeacherProfileService.Instance;

        // Cargar primero para poblar el cache
        _ = service.GetAsync(LearnerLevel.Student).GetAwaiter().GetResult();

        var version = service.GetProfileVersion(LearnerLevel.Student);

        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.Equal(16, version.Length); // Hash truncado a 16 caracteres hex
    }

    [Fact]
    public void GetProfileVersion_ConsistentAcrossCalls()
    {
        var service = TeacherProfileService.Instance;

        _ = service.GetAsync(LearnerLevel.University).GetAwaiter().GetResult();

        var v1 = service.GetProfileVersion(LearnerLevel.University);
        var v2 = service.GetProfileVersion(LearnerLevel.University);

        Assert.Equal(v1, v2);
    }

    [Fact]
    public void GetProfileVersion_DifferentLevels_DifferentHashes()
    {
        var service = TeacherProfileService.Instance;

        _ = service.GetAsync(LearnerLevel.Student).GetAwaiter().GetResult();
        _ = service.GetAsync(LearnerLevel.University).GetAwaiter().GetResult();

        var vStudent = service.GetProfileVersion(LearnerLevel.Student);
        var vUniversity = service.GetProfileVersion(LearnerLevel.University);

        Assert.NotEqual(vStudent, vUniversity);
    }

    [Fact]
    public async Task GetAsync_StudentLevel_DoesNotContainProgramFacts()
    {
        // El rules-file NO debe contener hechos del programa (unidades, temas, fechas)
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        // Verificar que NO mencione unidades específicas como hechos
        Assert.DoesNotContain("Unidad 1: Introducción a C#", content);
        Assert.DoesNotContain("semestre 2026-1", content);
    }

    [Fact]
    public async Task GetAsync_ContainsAdvertenciaNoContieneHechos()
    {
        var service = TeacherProfileService.Instance;
        var content = await service.GetAsync(LearnerLevel.Student);

        // El rules-file debe advertir que no contiene hechos del programa
        Assert.Contains("NO contiene", content);
        Assert.Contains("hechos del programa", content);
        Assert.Contains("SemesterContent", content);
    }
}
