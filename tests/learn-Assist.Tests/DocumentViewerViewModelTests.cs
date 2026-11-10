using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class DocumentViewerViewModelTests
{
    [Fact]
    public async Task OpenAsync_TextDocument_ShowsImportedContentAsPage()
    {
        LanguageService.Set("Español");
        var vm = new DocumentViewerViewModel();
        var document = new UserDocument
        {
            Name = "Notas",
            Type = "file",
            LocalPath = "notas.txt",
            Content = "Página de prueba",
        };

        await vm.OpenAsync(document);

        Assert.True(vm.IsOpen);
        Assert.Equal("Página de prueba", vm.CurrentPageText);
        Assert.Equal("Página 1 de 1", vm.PageIndicator);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void ExtractPdfPages_MissingFile_ReturnsExplicitError()
    {
        var result = FileContentReader.ExtractPdfPages("missing-file.pdf");

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Error);
    }
}
