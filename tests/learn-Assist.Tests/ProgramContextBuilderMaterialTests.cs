using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class ProgramContextBuilderMaterialTests
{
    [Fact]
    public async Task Build_SmallMaterials_IncludesAllDocuments()
    {
        var materials = new List<UserDocument>
        {
            new() { Name = "resumen.txt", Content = "Contenido del resumen sobre IA." },
            new() { Name = "apuntes.txt", Content = "Apuntes de clase sobre minería de datos." },
        };

        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, null, materials);

        Assert.Contains("### resumen.txt", context.SystemPrompt);
        Assert.Contains("### apuntes.txt", context.SystemPrompt);
        Assert.DoesNotContain("omitido por límite de contexto", context.SystemPrompt);
    }

    [Fact]
    public async Task Build_OversizedMaterials_TruncatesAndListsOmittedDocuments()
    {
        var materials = new List<UserDocument>
        {
            new() { Name = "libro-grande.txt", Content = new string('a', 400_000) },
            new() { Name = "libro-pequeno.txt", Content = "Texto pequeño que no cabe." },
        };

        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, null, materials);

        Assert.Contains("### libro-grande.txt", context.SystemPrompt);
        Assert.Contains("omitido por límite de contexto", context.SystemPrompt);
        Assert.Contains("### Documentos no incluidos por límite de contexto", context.SystemPrompt);
        Assert.Contains("- libro-pequeno.txt", context.SystemPrompt);

        // El prompt completo no debe dispararse por encima del presupuesto previsto.
        Assert.True(context.SystemPrompt.Length < 500_000);
    }

    [Fact]
    public async Task Build_EmptyContentDocuments_AreIgnored()
    {
        var materials = new List<UserDocument>
        {
            new() { Name = "vacio.pdf", Content = "" },
        };

        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.Student, null, materials);

        Assert.DoesNotContain("### vacio.pdf", context.SystemPrompt);
    }

    [Fact]
    public async Task Build_ResourcesOnly_AddsStrictGroundingRules()
    {
        var materials = new List<UserDocument>
        {
            new() { Name = "apuntes.pdf", Content = "La fotosíntesis ocurre en los cloroplastos." },
        };

        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, null, materials, resourcesOnly: true);

        Assert.Contains("MODO SOLO RECURSOS", context.SystemPrompt);
        Assert.Contains("única fuente de hechos", context.SystemPrompt);
        Assert.Contains("### apuntes.pdf", context.SystemPrompt);
        // La restricción debe leerse después del material (recencia).
        Assert.True(
            context.SystemPrompt.LastIndexOf("MODO SOLO RECURSOS", System.StringComparison.Ordinal)
            > context.SystemPrompt.LastIndexOf("### apuntes.pdf", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Build_ResourcesOnly_OmitsSemesterProgram()
    {
        const string program = "Unidad 1: Introducción al álgebra lineal.";
        var materials = new List<UserDocument>
        {
            new() { Name = "apuntes.pdf", Content = "Contenido de apoyo." },
        };

        var withProgram = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, program, materials);
        var strict = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, program, materials, resourcesOnly: true);

        Assert.Contains(program, withProgram.SystemPrompt);
        Assert.DoesNotContain(program, strict.SystemPrompt);
        Assert.Contains("### apuntes.pdf", strict.SystemPrompt);
    }

    [Fact]
    public async Task Build_WithoutResourcesOnly_DoesNotAddStrictRules()
    {
        var materials = new List<UserDocument>
        {
            new() { Name = "apuntes.pdf", Content = "Contenido de apoyo." },
        };

        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, null, materials);

        Assert.DoesNotContain("MODO SOLO RECURSOS", context.SystemPrompt);
    }

    [Fact]
    public async Task Build_ResourcesOnly_WithoutMaterials_WarnsAndForbidsGeneralKnowledge()
    {
        var context = await ProgramContextBuilder.Instance.BuildAsync(
            LearnerLevel.University, null, [], resourcesOnly: true);

        Assert.Contains("MODO SOLO RECURSOS", context.SystemPrompt);
        Assert.Contains("No hay recursos seleccionados", context.SystemPrompt);
        Assert.Contains("No puedes responder con conocimiento general", context.SystemPrompt);
    }
}
