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
}
