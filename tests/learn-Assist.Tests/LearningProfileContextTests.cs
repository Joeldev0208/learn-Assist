using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class LearningProfileContextTests
{
    [Fact]
    public void Build_NullProfile_ReturnsNull()
    {
        var result = LearningProfileContext.Build(null);
        Assert.Null(result);
    }

    [Fact]
    public void Build_EmptyProfile_ReturnsNull()
    {
        var profile = new LearningProfile { PreferredLanguage = string.Empty };
        var result = LearningProfileContext.Build(profile);
        Assert.Null(result);
    }

    [Fact]
    public void Build_ProfileWithMetadata_IncludesExpectedFields()
    {
        var profile = new LearningProfile
        {
            UserId = "user1",
            ProfileType = LearningProfileType.University,
            Course = "Ingeniería de Software",
            Level = "3ro",
            Career = "Ingeniería en Sistemas",
            Semester = "2026-1",
            University = "Universidad Nacional",
            Subjects = ["Programación", "Bases de Datos"],
            LearningGoal = "Aprender patrones de diseño",
            PreferredLanguage = "es",
        };

        var result = LearningProfileContext.Build(profile);

        Assert.NotNull(result);
        Assert.Contains("Estudiante universitario", result);
        Assert.Contains("Ingeniería en Sistemas", result);
        Assert.Contains("semestre 2026-1", result);
        Assert.Contains("en Universidad Nacional", result);
        Assert.Contains("Curso: Ingeniería de Software", result);
        Assert.Contains("Nivel: 3ro", result);
        Assert.Contains("Materias: Programación", result);
        Assert.Contains("Meta de aprendizaje: Aprender patrones de diseño", result);
        Assert.Contains("Idioma preferido: es", result);
    }

    [Fact]
    public void Build_ProfileWithManySubjects_TruncatesToMaxSix()
    {
        var profile = new LearningProfile
        {
            UserId = "user1",
            Subjects = ["A", "B", "C", "D", "E", "F", "G", "H"],
            PreferredLanguage = "es",
        };

        var result = LearningProfileContext.Build(profile);

        Assert.NotNull(result);
        Assert.Contains("Materias: A, B, C, D, E, F", result);
        Assert.Contains("y 2 más", result);
    }

    [Fact]
    public void Build_ProfileWithSemesterContent_IncludesProgramContent()
    {
        var profile = new LearningProfile
        {
            UserId = "user1",
            SemesterContent = "Unidad 1: Introducción a C#\nUnidad 2: Programación orientada a objetos",
            PreferredLanguage = "es",
        };

        var result = LearningProfileContext.Build(profile);

        Assert.NotNull(result);
        Assert.Contains("Unidad 1: Introducción a C#", result);
        Assert.Contains("Unidad 2: Programación orientada a objetos", result);
        Assert.Contains("Contenido del programa", result);
    }
}
